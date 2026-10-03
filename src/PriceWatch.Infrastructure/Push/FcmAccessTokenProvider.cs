using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Push;

public interface IFcmAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Obtém o token de acesso OAuth2 do Firebase a partir da conta de serviço: assina um JWT RS256
/// (escopo firebase.messaging) e o troca no <c>token_uri</c>. Sem SDK. O token é guardado em memória
/// e renovado 5 minutos antes de expirar.
/// </summary>
public class FcmAccessTokenProvider : IFcmAccessTokenProvider
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string DefaultTokenUri = "https://oauth2.googleapis.com/token";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan JwtLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly FcmSettings _settings;
    private readonly ILogger<FcmAccessTokenProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private DateTime _expiresAt = DateTime.MinValue;

    public FcmAccessTokenProvider(HttpClient httpClient, IOptions<FcmSettings> settings, ILogger<FcmAccessTokenProvider> logger)
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

            var json = _settings.ResolveServiceAccountJson()
                ?? throw new InvalidOperationException("FCM service account is not configured.");
            var account = JsonSerializer.Deserialize<ServiceAccount>(json)
                ?? throw new InvalidOperationException("FCM service account JSON is invalid.");

            var tokenUri = string.IsNullOrWhiteSpace(account.TokenUri) ? DefaultTokenUri : account.TokenUri;
            var assertion = BuildSignedJwt(account, tokenUri);

            using var response = await _httpClient.PostAsync(tokenUri, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", assertion)
            }), cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var token = await JsonSerializer.DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Empty FCM token response.");

            _cachedToken = token.AccessToken;
            _expiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            _logger.LogInformation("Obtained a new FCM access token.");
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string BuildSignedJwt(ServiceAccount account, string audience)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var claims = JsonSerializer.Serialize(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = audience,
            iat = issuedAt.ToUnixTimeSeconds(),
            exp = issuedAt.Add(JwtLifetime).ToUnixTimeSeconds()
        });

        var unsigned = $"{Base64Url(Encoding.UTF8.GetBytes(header))}.{Base64Url(Encoding.UTF8.GetBytes(claims))}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private record ServiceAccount(
        [property: JsonPropertyName("client_email")] string ClientEmail,
        [property: JsonPropertyName("private_key")] string PrivateKey,
        [property: JsonPropertyName("token_uri")] string? TokenUri);

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
