using PriceWatch.Application.Interfaces;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.Services;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Application.UseCases.Discovery;

/// <summary>
/// Procura o produto acompanhado em todas as lojas disponíveis, inclusive a de origem (outras ofertas
/// do mesmo tipo de produto): GTIN primeiro (alta confiança) e, sem resultado útil, por palavra-chave. Cada resultado é pontuado (<see cref="CandidateScorer"/>) e as aprovadas viram
/// <see cref="OfferCandidate"/>: NÃO são monitoradas nem alertam até o usuário confirmar (RI08).
/// Roda no máximo uma vez por produto por janela de tempo, compartilhada entre usuários (RNF19).
/// </summary>
public class DiscoverOffersUseCase
{
    private static readonly TimeSpan DiscoveryWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan CandidateTimeToLive = TimeSpan.FromHours(24);
    private const int MaxCandidatesPerStore = 3;
    private const int MaxCandidatesToJudge = 6;

    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IOfferCandidateRepository _candidates;
    private readonly IEnumerable<IOfferSearcher> _searchers;
    private readonly IDiscoveryGate _gate;
    private readonly IMatchJudge _judge;

    public DiscoverOffersUseCase(
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IStoreOfferRepository offers,
        IOfferCandidateRepository candidates,
        IEnumerable<IOfferSearcher> searchers,
        IDiscoveryGate gate,
        IMatchJudge judge)
    {
        _subscriptions = subscriptions;
        _products = products;
        _offers = offers;
        _candidates = candidates;
        _searchers = searchers;
        _gate = gate;
        _judge = judge;
    }

    /// <returns>True se a busca rodou agora; false se já tinha rodado dentro da janela.</returns>
    public async Task<bool> ExecuteAsync(string userId, string productId)
    {
        if (await _subscriptions.GetByUserIdAndProductIdAsync(userId, productId) is null)
            throw new ProductNotFoundException(productId);

        var product = await _products.GetByIdAsync(productId)
            ?? throw new ProductNotFoundException(productId);

        if (!await _gate.TryAcquireAsync(productId, DiscoveryWindow))
            return false;

        var ownOffers = (await _offers.GetByProductIdAsync(productId)).ToList();
        var trackedUrls = ownOffers.Select(o => o.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // a busca por palavra-chave só roda quando o GTIN não resolve; a IA (se ligada) tenta montar uma busca melhor
        var query = new Lazy<Task<string>>(() => BuildQueryAsync(product));

        var stores = _searchers.Where(s => s.IsAvailable).ToList();

        // as buscas nas lojas rodam em paralelo; a gravação é sequencial
        var found = await Task.WhenAll(stores.Select(async store => (Store: store, Results: await SearchAsync(store, product, query, trackedUrls))));

        foreach (var (store, results) in found)
            await SaveCandidatesAsync(product, store.ProductSource, results, trackedUrls);

        return true;
    }

    private static async Task<IReadOnlyList<OfferSearchResult>> SearchAsync(
        IOfferSearcher store, Product product, Lazy<Task<string>> query, HashSet<string> trackedUrls)
    {
        if (product.Gtin is not null)
        {
            // na loja de origem a busca por GTIN devolve o próprio produto: sem outro resultado, cai para a palavra-chave
            var byGtin = (await store.SearchByGtinAsync(product.Gtin)).Where(r => !IsTracked(r, trackedUrls)).ToList();
            if (byGtin.Count > 0)
                return byGtin;
        }

        return await store.SearchByKeywordAsync(await query.Value);
    }

    private async Task<string> BuildQueryAsync(Product product)
    {
        if (_judge.IsAvailable)
        {
            var suggested = (await _judge.SuggestSearchQueryAsync(product))?.Trim();
            if (!string.IsNullOrEmpty(suggested))
                return suggested;
        }

        return SearchQueryBuilder.Build(product);
    }

    private static bool IsTracked(OfferSearchResult result, HashSet<string> trackedUrls)
    {
        var normalized = Normalize(result);
        return normalized is not null && trackedUrls.Contains(normalized.Url);
    }

    private async Task SaveCandidatesAsync(
        Product product, ProductSource source, IReadOnlyList<OfferSearchResult> results, HashSet<string> trackedUrls)
    {
        var scored = results
            .Select(Normalize)
            .Where(r => r is not null && !trackedUrls.Contains(r.Url))
            .Cast<OfferSearchResult>()
            .DistinctBy(r => r.Url, StringComparer.OrdinalIgnoreCase)
            .Select(r => (Result: r, Score: CandidateScorer.Score(product, r), Ai: (MatchJudgement?)null))
            .Where(x => x.Score.Accepted)
            .OrderByDescending(x => x.Score.Score)
            .Take(MaxCandidatesToJudge)
            .ToList();

        var accepted = (await JudgeAsync(product, scored))
            .OrderByDescending(x => x.Ai?.SameProduct == true)
            .ThenByDescending(x => x.Score.Score)
            .Take(MaxCandidatesPerStore);

        foreach (var (result, score, ai) in accepted)
        {
            var candidate = await _candidates.GetByKeyAsync(product.Id, source, result.Url);
            if (candidate is null)
                candidate = OfferCandidate.Create(product.Id, source, result, score.Score, score.Reason, CandidateTimeToLive);
            else
                candidate.Refresh(result, score.Score, score.Reason, CandidateTimeToLive);

            if (ai is not null)
                candidate.ApplyJudgement(ai.SameProduct, ai.Reason);

            await _candidates.SaveAsync(candidate);
        }
    }

    /// <summary>
    /// Segunda opinião da IA só para quem as regras aprovaram SEM GTIN idêntico. A IA pode descartar ou
    /// confirmar; se não responder, ninguém é descartado (as regras e o usuário seguem decidindo).
    /// </summary>
    private async Task<List<(OfferSearchResult Result, CandidateScore Score, MatchJudgement? Ai)>> JudgeAsync(
        Product product, List<(OfferSearchResult Result, CandidateScore Score, MatchJudgement? Ai)> scored)
    {
        var toJudge = scored.Where(x => x.Score.Reason != CandidateReason.Gtin).ToList();
        if (!_judge.IsAvailable || toJudge.Count == 0)
            return scored;

        var verdicts = await _judge.JudgeAsync(product, toJudge.Select(x => x.Result).ToList());
        if (verdicts is null)
            return scored;

        var byUrl = new Dictionary<string, MatchJudgement>(StringComparer.OrdinalIgnoreCase);
        foreach (var verdict in verdicts)
        {
            if (verdict.Index >= 0 && verdict.Index < toJudge.Count)
                byUrl[toJudge[verdict.Index].Result.Url] = verdict;
        }

        return scored
            .Select(x => byUrl.TryGetValue(x.Result.Url, out var ai) ? x with { Ai = ai } : x)
            .Where(x => x.Ai is null || x.Ai.SameProduct)
            .ToList();
    }

    // a URL da candidata vira a URL da StoreOffer se confirmada: precisa estar normalizada como as importadas
    private static OfferSearchResult? Normalize(OfferSearchResult result)
    {
        try
        {
            return result with { Url = UrlNormalizer.Normalize(result.Url) };
        }
        catch (BusinessException)
        {
            return null;
        }
    }
}
