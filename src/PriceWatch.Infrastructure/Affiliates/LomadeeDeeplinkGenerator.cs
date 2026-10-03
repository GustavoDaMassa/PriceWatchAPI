using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Affiliates;

/// <summary>
/// Converte a URL original do Kabum num link de afiliado rastreado ("lomadeezado") via
/// Deeplink API da Lomadee/SocialSoul. Mesma ressalva do <see cref="Fetchers.KabumFetcher"/>:
/// caminho do endpoint documentado, não validado contra a API real ainda.
///
/// Nunca lança — se a chamada falhar, cai no passthrough (retorna a URL original). Perder o
/// rastreamento de uma comissão é melhor do que quebrar o redirect do usuário pra loja.
/// </summary>
public class LomadeeDeeplinkGenerator : IAffiliateLinkGenerator
{
    private readonly HttpClient _httpClient;
    private readonly LomadeeSettings _settings;
    private readonly ILogger<LomadeeDeeplinkGenerator> _logger;

    public ProductSource ProductSource => ProductSource.Kabum;

    public bool IsAvailable => _settings.IsConfigured;

    public bool CanHandle(string url) =>
        _settings.IsConfigured && url.Contains("kabum.com.br", StringComparison.OrdinalIgnoreCase);

    public LomadeeDeeplinkGenerator(HttpClient httpClient, IOptions<LomadeeSettings> settings, ILogger<LomadeeDeeplinkGenerator> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string originalUrl)
    {
        try
        {
            var requestUri =
                $"{_settings.BaseUrl}/{_settings.AppToken}/deeplink/_create" +
                $"?sourceId={Uri.EscapeDataString(_settings.SourceId ?? string.Empty)}" +
                $"&url={Uri.EscapeDataString(originalUrl)}" +
                "&format=json";

            var response = await _httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Lomadee deeplink API returned {StatusCode} for '{Url}' — falling back to original URL.",
                    (int)response.StatusCode, originalUrl);
                return originalUrl;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var result = await JsonSerializer.DeserializeAsync<LomadeeDeeplinkResult>(stream);

            return result?.ShortUrl ?? originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate Lomadee deeplink for '{Url}' — falling back to original URL.", originalUrl);
            return originalUrl;
        }
    }

    private record LomadeeDeeplinkResult(
        [property: JsonPropertyName("shortUrl")] string? ShortUrl);
}
