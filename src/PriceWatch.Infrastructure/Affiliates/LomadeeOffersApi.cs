using System.Text.Json;
using System.Text.Json.Serialization;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Affiliates;

/// <summary>
/// Chamada à API de ofertas da Lomadee/SocialSoul (<c>/offer/_search</c>), compartilhada pelo
/// <c>KabumFetcher</c> (preço de uma URL) e pelo <c>KabumOfferSearcher</c> (descoberta). Mesma
/// ressalva dos dois: caminho e campos seguem a documentação pública e ainda não foram validados
/// contra a API real (a Lomadee exige cadastro de afiliado aprovado).
/// </summary>
internal static class LomadeeOffersApi
{
    public static async Task<IReadOnlyList<LomadeeOffer>> SearchAsync(
        HttpClient httpClient, LomadeeSettings settings, string keyword)
    {
        var requestUri =
            $"{settings.BaseUrl}/{settings.AppToken}/offer/_search" +
            $"?sourceId={Uri.EscapeDataString(settings.SourceId ?? string.Empty)}" +
            $"&keyword={Uri.EscapeDataString(keyword)}" +
            "&format=json";

        var response = await httpClient.GetAsync(requestUri);
        if (!response.IsSuccessStatusCode)
            throw new BusinessException($"Lomadee API returned {(int)response.StatusCode} for Kabum search '{keyword}'.");

        await using var stream = await response.Content.ReadAsStreamAsync();
        var result = await JsonSerializer.DeserializeAsync<LomadeeOfferSearchResult>(stream)
            ?? throw new BusinessException("Failed to parse Lomadee API response.");

        return result.Offers ?? [];
    }

    private record LomadeeOfferSearchResult(
        [property: JsonPropertyName("offers")] List<LomadeeOffer>? Offers);
}

internal record LomadeeOffer(
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("barcode")] string? Barcode,
    [property: JsonPropertyName("images")] List<string>? Images,
    [property: JsonPropertyName("prices")] List<LomadeePrice>? Prices);

internal record LomadeePrice(
    [property: JsonPropertyName("price")] decimal Price);
