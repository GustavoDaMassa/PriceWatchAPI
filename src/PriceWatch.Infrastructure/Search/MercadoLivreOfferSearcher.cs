using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;
using PriceWatch.Infrastructure.Fetchers;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Search;

/// <summary>
/// Descoberta no Mercado Livre pela API de catálogo (<c>/products/search</c>, por
/// <c>product_identifier</c> = GTIN ou por <c>q</c> = palavra-chave). Cada produto de catálogo
/// encontrado vira <c>/p/MLB...</c> e o preço/detalhes vêm do mesmo caminho que o monitoramento usa
/// (<c>MercadoLivreFetcher</c>), então a oferta confirmada é monitorável sem adaptação. NÃO validado
/// contra a API real (parâmetros conforme a documentação de catálogo do ML).
/// </summary>
public partial class MercadoLivreOfferSearcher : IOfferSearcher
{
    private const int MaxProducts = 20;
    private const string SearchUrl = "https://api.mercadolibre.com/products/search?status=active&site_id=MLB&limit=20";

    private readonly HttpClient _httpClient;
    private readonly IMercadoLivreTokenProvider _tokens;
    private readonly MercadoLivreSettings _settings;
    private readonly IPriceFetcher _catalogFetcher;
    private readonly ILogger<MercadoLivreOfferSearcher> _logger;

    public MercadoLivreOfferSearcher(
        HttpClient httpClient,
        IMercadoLivreTokenProvider tokens,
        IOptions<MercadoLivreSettings> settings,
        IPriceFetcher catalogFetcher,
        ILogger<MercadoLivreOfferSearcher> logger)
    {
        _httpClient = httpClient;
        _tokens = tokens;
        _settings = settings.Value;
        _catalogFetcher = catalogFetcher;
        _logger = logger;
    }

    public ProductSource ProductSource => ProductSource.MercadoLivre;

    public bool IsAvailable => _settings.IsConfigured;

    public Task<IReadOnlyList<OfferSearchResult>> SearchByGtinAsync(string gtin) =>
        SearchAsync($"{SearchUrl}&product_identifier={Uri.EscapeDataString(gtin.Trim())}");

    public Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordAsync(string query) =>
        SearchByKeywordPageAsync(query, 0);

    public Task<IReadOnlyList<OfferSearchResult>> SearchByKeywordPageAsync(string query, int page) =>
        string.IsNullOrWhiteSpace(query) || page < 0
            ? Task.FromResult<IReadOnlyList<OfferSearchResult>>([])
            : SearchAsync($"{SearchUrl}&offset={page * MaxProducts}&q={Uri.EscapeDataString(query.Trim())}");

    private async Task<IReadOnlyList<OfferSearchResult>> SearchAsync(string url)
    {
        if (!IsAvailable)
            return [];

        try
        {
            var token = await _tokens.GetTokenAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mercado Livre catalog search returned {StatusCode}.", (int)response.StatusCode);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var body = await JsonSerializer.DeserializeAsync<CatalogSearchResponse>(stream);

            var catalogIds = (body?.Results ?? [])
                .Select(r => r.Id)
                .Where(id => id is not null && CatalogIdRegex().IsMatch(id))
                .Take(MaxProducts)
                .ToList();

            var found = await Task.WhenAll(catalogIds.Select(id => ReadCatalogProductAsync(id!)));
            return found.Where(r => r is not null).Cast<OfferSearchResult>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mercado Livre offer search failed.");
            return [];
        }
    }

    private async Task<OfferSearchResult?> ReadCatalogProductAsync(string catalogId)
    {
        var url = $"https://www.mercadolivre.com.br/p/{catalogId}";
        try
        {
            var product = await _catalogFetcher.FetchAsync(url);
            return new OfferSearchResult(url, product.Name, product.Price, product.ImageUrl, product.Gtin, product.Brand, product.Model);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Skipping Mercado Livre catalog product {CatalogId}.", catalogId);
            return null;
        }
    }

    [GeneratedRegex(@"^MLB\d+$")]
    private static partial Regex CatalogIdRegex();

    private record CatalogSearchResponse(
        [property: JsonPropertyName("results")] List<CatalogSearchEntry>? Results);

    private record CatalogSearchEntry(
        [property: JsonPropertyName("id")] string? Id);
}
