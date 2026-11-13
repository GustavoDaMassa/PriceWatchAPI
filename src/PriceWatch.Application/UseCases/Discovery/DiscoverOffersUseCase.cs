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
/// A "nova busca" (<c>findNew</c>) ignora a janela de 6 horas, mas tem uma pausa curta própria, e traz só o que
/// ainda não apareceu: ignora candidatas anteriores, o que o usuário já julgou e o que ele já acompanha, e anda
/// pelas próximas páginas de resultado quando a primeira não tem nada novo.
/// </summary>
public class DiscoverOffersUseCase
{
    private static readonly TimeSpan DiscoveryWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan CandidateTimeToLive = TimeSpan.FromHours(24);
    private const int MaxCandidatesPerStore = 3;
    private const int MaxCandidatesToJudge = 6;
    private const int MaxPagesWhenFindingNew = 3;
    private static readonly TimeSpan FindNewCooldown = TimeSpan.FromSeconds(45);

    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IOfferCandidateRepository _candidates;
    private readonly IOfferMatchFeedbackRepository _feedback;
    private readonly IEnumerable<IOfferSearcher> _searchers;
    private readonly IDiscoveryGate _gate;
    private readonly IMatchJudge _judge;

    public DiscoverOffersUseCase(
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IStoreOfferRepository offers,
        IOfferCandidateRepository candidates,
        IOfferMatchFeedbackRepository feedback,
        IEnumerable<IOfferSearcher> searchers,
        IDiscoveryGate gate,
        IMatchJudge judge)
    {
        _subscriptions = subscriptions;
        _products = products;
        _offers = offers;
        _candidates = candidates;
        _feedback = feedback;
        _searchers = searchers;
        _gate = gate;
        _judge = judge;
    }

    /// <returns>True se a busca rodou agora; false se já tinha rodado dentro da janela (ou, na nova busca, da pausa).</returns>
    public async Task<bool> ExecuteAsync(string userId, string productId, bool findNew = false)
    {
        if (await _subscriptions.GetByUserIdAndProductIdAsync(userId, productId) is null)
            throw new ProductNotFoundException(productId);

        var product = await _products.GetByIdAsync(productId)
            ?? throw new ProductNotFoundException(productId);

        var acquired = findNew
            ? await _gate.TryAcquireAsync($"{productId}:new", FindNewCooldown)
            : await _gate.TryAcquireAsync(productId, DiscoveryWindow);
        if (!acquired)
            return false;

        var excludedUrls = await ExcludedUrlsAsync(userId, product, findNew);
        // a busca por palavra-chave só roda quando o GTIN não resolve; a IA (se ligada) tenta montar uma busca melhor
        var query = new Lazy<Task<string>>(() => BuildQueryAsync(product));

        var stores = _searchers.Where(s => s.IsAvailable).ToList();
        var pages = findNew ? MaxPagesWhenFindingNew : 1;

        for (var page = 0; page < pages; page++)
        {
            // as buscas nas lojas rodam em paralelo; a gravação é sequencial
            var found = await Task.WhenAll(stores.Select(async store =>
                (Store: store, Results: await SearchAsync(store, product, query, excludedUrls, findNew, page))));

            var saved = 0;
            foreach (var (store, results) in found)
                saved += await SaveCandidatesAsync(product, store.ProductSource, results, excludedUrls);

            // achou algo novo, ou as lojas não têm mais resultados: não precisa ir para a página seguinte
            if (saved > 0 || found.All(f => f.Results.Count == 0))
                break;
        }

        return true;
    }

    /// <summary>
    /// URLs que não devem voltar como sugestão: as ofertas do produto e tudo que o usuário já acompanha (em qualquer
    /// produto dele). Na nova busca, também as candidatas que já apareceram e as que o usuário já julgou.
    /// </summary>
    private async Task<HashSet<string>> ExcludedUrlsAsync(string userId, Product product, bool findNew)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var offer in await _offers.GetByProductIdAsync(product.Id))
            urls.Add(offer.Url);

        var userProductIds = (await _subscriptions.GetByUserIdAsync(userId)).Select(s => s.ProductId).Distinct().ToList();
        if (userProductIds.Count > 0)
        {
            foreach (var offer in await _offers.GetByProductIdsAsync(userProductIds))
                urls.Add(offer.Url);
        }

        if (findNew)
        {
            foreach (var candidate in await _candidates.GetByProductIdAsync(product.Id))
                urls.Add(candidate.Url);

            foreach (var verdict in await _feedback.GetByUserAndProductAsync(userId, product.Id))
                urls.Add(verdict.Url);
        }

        return urls;
    }

    private static async Task<IReadOnlyList<OfferSearchResult>> SearchAsync(
        IOfferSearcher store, Product product, Lazy<Task<string>> query, HashSet<string> trackedUrls, bool findNew, int page)
    {
        // a nova busca anda pelas páginas da busca por palavra-chave (o GTIN só tem uma "página": a primeira busca já o usou)
        if (findNew)
            return await store.SearchByKeywordPageAsync(await query.Value, page);

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

    private async Task<int> SaveCandidatesAsync(
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

        var savedCount = 0;
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
            savedCount++;
        }

        return savedCount;
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
