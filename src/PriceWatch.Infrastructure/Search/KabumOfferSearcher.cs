using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.Services;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.Infrastructure.Affiliates;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Search;

/// <summary>
/// Descoberta de ofertas da Kabum pela API de ofertas da Lomadee (busca por palavra-chave). A busca
/// por GTIN usa o próprio GTIN como palavra-chave e só aceita resultados cujo <c>barcode</c> bate
/// exatamente. NÃO validado contra a API real: se o índice da Lomadee não casar por código de
/// barras, a busca por GTIN volta vazia e a descoberta cai na busca por palavra-chave.
/// </summary>
public class KabumOfferSearcher : IOfferSearcher
{
    private const int MaxResults = 5;
    private const string StoreHost = "kabum.com.br";

    private readonly HttpClient _httpClient;
    private readonly LomadeeSettings _settings;
    private readonly ILogger<KabumOfferSearcher> _logger;

    public KabumOfferSearcher(HttpClient httpClient, IOptions<LomadeeSettings> settings, ILogger<KabumOfferSearcher> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public ProductSource ProductSource => ProductSource.Kabum;

    public bool IsAvailable => _settings.IsConfigured;

    public async Task<IReadOnlyList<OfferSearchResult>> SearchByGtinAsync(string gtin)
    {
        var wanted = Normalize(gtin);
        if (wanted is null)
            return [];

        var results = await SearchAsync(gtin);
        return results.Where(r => Normalize(r.Gtin) == wanted).ToList();
    }

    public Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordAsync(string query) => SearchAsync(query);

    private async Task<IReadOnlyList<OfferSearchResult>> SearchAsync(string keyword)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(keyword))
            return [];

        try
        {
            var offers = await LomadeeOffersApi.SearchAsync(_httpClient, _settings, keyword.Trim());

            return offers
                .Where(o => o.Link is not null && o.Link.Contains(StoreHost, StringComparison.OrdinalIgnoreCase))
                .Select(ToResult)
                .Where(r => r is not null)
                .Cast<OfferSearchResult>()
                .Take(MaxResults)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kabum offer search failed for '{Query}'.", keyword);
            return [];
        }
    }

    private static OfferSearchResult? ToResult(LomadeeOffer offer)
    {
        var price = offer.Prices?.Where(p => p.Price > 0).Min(p => (decimal?)p.Price);
        if (price is null || string.IsNullOrWhiteSpace(offer.Description))
            return null;

        return new OfferSearchResult(
            Url: offer.Link!,
            Name: offer.Description,
            Price: price.Value,
            ImageUrl: offer.Images?.FirstOrDefault(),
            Gtin: GtinNormalizer.Normalize(offer.Barcode));
    }

    private static string? Normalize(string? gtin) => GtinNormalizer.Normalize(gtin)?.TrimStart('0');
}
