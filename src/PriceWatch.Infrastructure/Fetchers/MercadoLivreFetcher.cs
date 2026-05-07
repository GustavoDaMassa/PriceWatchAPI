using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.ValueObjects;

namespace PriceWatch.Infrastructure.Fetchers;

public partial class MercadoLivreFetcher : IPriceFetcher
{
    private readonly HttpClient _httpClient;

    public ProductSource ProductSource => ProductSource.MercadoLivre;

    public bool CanHandle(string url) =>
        url.Contains("mercadolivre.com.br", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("mercadolibre.com", StringComparison.OrdinalIgnoreCase);

    public MercadoLivreFetcher(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProductFetchResult> FetchAsync(string url)
    {
        var match = ItemIdRegex().Match(url);
        if (!match.Success)
            throw new BusinessException("Invalid Mercado Livre URL: could not extract item ID.");

        var itemId = $"MLB{match.Groups[1].Value}";

        var response = await _httpClient.GetAsync($"https://api.mercadolibre.com/items/{itemId}");
        if (!response.IsSuccessStatusCode)
            throw new BusinessException($"Mercado Livre API returned {(int)response.StatusCode} for item '{itemId}'.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        var item = await JsonSerializer.DeserializeAsync<MercadoLivreItem>(stream);

        if (item is null || item.Price <= 0)
            throw new BusinessException($"Could not read a valid price for item '{itemId}'.");

        return new ProductFetchResult(item.Price, item.Title);
    }

    [GeneratedRegex(@"MLB-?(\d+)", RegexOptions.Compiled)]
    private static partial Regex ItemIdRegex();

    private record MercadoLivreItem(
        [property: JsonPropertyName("price")] decimal Price,
        [property: JsonPropertyName("title")] string Title);
}
