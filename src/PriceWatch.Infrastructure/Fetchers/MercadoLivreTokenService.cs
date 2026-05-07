using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Fetchers;

public class MercadoLivreTokenService
{
    private readonly HttpClient _httpClient;
    private readonly MercadoLivreSettings _settings;
    private readonly ILogger<MercadoLivreTokenService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTime _expiresAt = DateTime.MinValue;

    private const string TokenUrl = "https://api.mercadolibre.com/oauth/token";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);

    public MercadoLivreTokenService(
        HttpClient httpClient,
        IOptions<MercadoLivreSettings> settings,
        ILogger<MercadoLivreTokenService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _expiresAt - RefreshBuffer)
            return _cachedToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _expiresAt - RefreshBuffer)
                return _cachedToken;

            _logger.LogInformation("Fetching new Mercado Livre access token.");

            var body = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _settings.ClientId),
                new KeyValuePair<string, string>("client_secret", _settings.ClientSecret)
            ]);

            var response = await _httpClient.PostAsync(TokenUrl, body, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken);

            _cachedToken = result!.AccessToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
