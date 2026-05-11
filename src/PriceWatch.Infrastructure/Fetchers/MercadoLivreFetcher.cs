using System.Net.Http.Headers;
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
    private readonly MercadoLivreTokenService _tokenService;

    public ProductSource ProductSource => ProductSource.MercadoLivre;

    public bool CanHandle(string url) =>
        url.Contains("mercadolivre.com.br", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("mercadolibre.com", StringComparison.OrdinalIgnoreCase);

    public MercadoLivreFetcher(HttpClient httpClient, MercadoLivreTokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    public async Task<ProductFetchResult> FetchAsync(string url)
    {
        var catalogMatch = CatalogIdRegex().Match(url);
        if (!catalogMatch.Success)
        {
            if (ItemIdRegex().IsMatch(url))
                throw new BusinessException(
                    "Invalid Mercado Livre URL: please use the catalog page URL (containing /p/MLB...).");
            throw new BusinessException("Invalid Mercado Livre URL: could not extract product ID.");
        }

        var catalogId = $"MLB{catalogMatch.Groups[1].Value}";
        var token = await _tokenService.GetTokenAsync();

        var productTask = GetAsync<MercadoLivreProduct>(
            $"https://api.mercadolibre.com/products/{catalogId}", token);
        var itemsTask = GetAsync<MercadoLivreItemsResult>(
            $"https://api.mercadolibre.com/products/{catalogId}/items", token);

        await Task.WhenAll(productTask, itemsTask);

        var product = await productTask;
        var items = await itemsTask;

        var minPrice = items.Results?
            .Where(i => i.Price > 0)
            .Min(i => (decimal?)i.Price) ?? 0;

        if (minPrice <= 0)
            throw new BusinessException($"Could not read a valid price for catalog product '{catalogId}'.");

        var imageUrl = product.Pictures?.FirstOrDefault()?.Url;

        return new ProductFetchResult(minPrice, product.Name ?? catalogId, imageUrl);
    }

    private async Task<T> GetAsync<T>(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new BusinessException(
                $"Mercado Livre API returned {(int)response.StatusCode} for '{url}'.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream)
               ?? throw new BusinessException("Failed to parse Mercado Livre API response.");
    }

    // Matches catalog page URLs: /p/MLB123456
    [GeneratedRegex(@"/p/MLB(\d+)", RegexOptions.Compiled)]
    private static partial Regex CatalogIdRegex();

    // Detects item-specific URLs (no /p/ prefix): MLB-123456 or MLB123456
    [GeneratedRegex(@"(?<!/p/)MLB-?\d+", RegexOptions.Compiled)]
    private static partial Regex ItemIdRegex();

    private record MercadoLivreProduct(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("pictures")] List<MercadoLivrePicture>? Pictures);

    private record MercadoLivrePicture(
        [property: JsonPropertyName("url")] string? Url);

    private record MercadoLivreItemsResult(
        [property: JsonPropertyName("results")] List<MercadoLivreItemEntry>? Results);

    private record MercadoLivreItemEntry(
        [property: JsonPropertyName("price")] decimal Price);
}
