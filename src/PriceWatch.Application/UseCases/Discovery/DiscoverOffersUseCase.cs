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
/// Procura o produto acompanhado nas outras lojas: GTIN primeiro (alta confiança) e, sem resultado,
/// por palavra-chave. Cada resultado é pontuado (<see cref="CandidateScorer"/>) e as aprovadas viram
/// <see cref="OfferCandidate"/>: NÃO são monitoradas nem alertam até o usuário confirmar (RI08).
/// Roda no máximo uma vez por produto por janela de tempo, compartilhada entre usuários (RNF19).
/// </summary>
public class DiscoverOffersUseCase
{
    private static readonly TimeSpan DiscoveryWindow = TimeSpan.FromHours(6);
    private static readonly TimeSpan CandidateTimeToLive = TimeSpan.FromHours(24);
    private const int MaxCandidatesPerStore = 3;

    private readonly IUserProductSubscriptionRepository _subscriptions;
    private readonly IProductRepository _products;
    private readonly IStoreOfferRepository _offers;
    private readonly IOfferCandidateRepository _candidates;
    private readonly IEnumerable<IOfferSearcher> _searchers;
    private readonly IDiscoveryGate _gate;

    public DiscoverOffersUseCase(
        IUserProductSubscriptionRepository subscriptions,
        IProductRepository products,
        IStoreOfferRepository offers,
        IOfferCandidateRepository candidates,
        IEnumerable<IOfferSearcher> searchers,
        IDiscoveryGate gate)
    {
        _subscriptions = subscriptions;
        _products = products;
        _offers = offers;
        _candidates = candidates;
        _searchers = searchers;
        _gate = gate;
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
        var coveredSources = ownOffers.Select(o => o.Source).ToHashSet();
        var trackedUrls = ownOffers.Select(o => o.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var query = SearchQueryBuilder.Build(product);

        var stores = _searchers.Where(s => s.IsAvailable && !coveredSources.Contains(s.ProductSource)).ToList();

        // as buscas nas lojas rodam em paralelo; a gravação é sequencial
        var found = await Task.WhenAll(stores.Select(async store => (Store: store, Results: await SearchAsync(store, product, query))));

        foreach (var (store, results) in found)
            await SaveCandidatesAsync(product, store.ProductSource, results, trackedUrls);

        return true;
    }

    private static async Task<IReadOnlyList<OfferSearchResult>> SearchAsync(IOfferSearcher store, Product product, string query)
    {
        if (product.Gtin is not null)
        {
            var byGtin = await store.SearchByGtinAsync(product.Gtin);
            if (byGtin.Count > 0)
                return byGtin;
        }

        return await store.SearchByKeywordAsync(query);
    }

    private async Task SaveCandidatesAsync(
        Product product, ProductSource source, IReadOnlyList<OfferSearchResult> results, HashSet<string> trackedUrls)
    {
        var accepted = results
            .Select(Normalize)
            .Where(r => r is not null && !trackedUrls.Contains(r.Url))
            .Cast<OfferSearchResult>()
            .DistinctBy(r => r.Url, StringComparer.OrdinalIgnoreCase)
            .Select(r => (Result: r, Score: CandidateScorer.Score(product, r)))
            .Where(x => x.Score.Accepted)
            .OrderByDescending(x => x.Score.Score)
            .Take(MaxCandidatesPerStore);

        foreach (var (result, score) in accepted)
        {
            var candidate = await _candidates.GetByKeyAsync(product.Id, source, result.Url);
            if (candidate is null)
                candidate = OfferCandidate.Create(product.Id, source, result, score.Score, score.Reason, CandidateTimeToLive);
            else
                candidate.Refresh(result, score.Score, score.Reason, CandidateTimeToLive);

            await _candidates.SaveAsync(candidate);
        }
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
