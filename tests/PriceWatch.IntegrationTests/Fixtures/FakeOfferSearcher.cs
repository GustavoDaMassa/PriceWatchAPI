using System.Collections.Concurrent;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.IntegrationTests.Fixtures;

/// <summary>Buscador falso da Kabum: o teste define o que cada GTIN/consulta devolve e conta as chamadas.</summary>
public class FakeOfferSearcher : IOfferSearcher
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<OfferSearchResult>> _byGtin = new();
    private int _gtinCalls;
    private int _keywordCalls;

    public ProductSource ProductSource => ProductSource.Kabum;
    public bool IsAvailable => true;

    public Func<string, IReadOnlyList<OfferSearchResult>> KeywordHandler { get; set; } = _ => [];

    public int GtinCalls => _gtinCalls;
    public int KeywordCalls => _keywordCalls;

    public void SetResultsForGtin(string gtin, params OfferSearchResult[] results) => _byGtin[gtin] = results;

    public Task<IReadOnlyList<OfferSearchResult>> SearchByGtinAsync(string gtin)
    {
        Interlocked.Increment(ref _gtinCalls);
        return Task.FromResult(_byGtin.TryGetValue(gtin, out var results) ? results : []);
    }

    public Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordAsync(string query)
    {
        Interlocked.Increment(ref _keywordCalls);
        return Task.FromResult(KeywordHandler(query));
    }
}
