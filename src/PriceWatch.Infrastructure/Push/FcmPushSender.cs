using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Push;

/// <summary>
/// Push pelo Firebase Cloud Messaging (HTTP v1). Nunca lança: qualquer problema vira
/// <see cref="PushResult.Failed"/>, e só um token que o Firebase diz não reconhecer vira
/// <see cref="PushResult.InvalidToken"/> (para o chamador descartá-lo). Não validado contra o
/// Firebase real: precisa de um projeto Firebase e de uma conta de serviço (Fase 0).
/// </summary>
public class FcmPushSender : IPushSender
{
    private readonly HttpClient _httpClient;
    private readonly IFcmAccessTokenProvider _tokens;
    private readonly FcmSettings _settings;
    private readonly ILogger<FcmPushSender> _logger;

    public FcmPushSender(
        HttpClient httpClient,
        IFcmAccessTokenProvider tokens,
        IOptions<FcmSettings> settings,
        ILogger<FcmPushSender> logger)
    {
        _httpClient = httpClient;
        _tokens = tokens;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsAvailable => _settings.IsConfigured;

    public async Task<PushResult> SendAsync(string deviceToken, PushMessage message)
    {
        if (!IsAvailable)
            return PushResult.Failed;

        try
        {
            var accessToken = await _tokens.GetTokenAsync();

            var payload = JsonSerializer.Serialize(new
            {
                message = new
                {
                    token = deviceToken,
                    notification = new { title = message.Title, body = message.Body },
                    data = message.Data ?? new Dictionary<string, string>(),
                    android = new { priority = "HIGH" }
                }
            });

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_settings.ProjectId}/messages:send")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return PushResult.Sent;

            var body = await response.Content.ReadAsStringAsync();
            if (IsUnknownToken(response.StatusCode, body))
                return PushResult.InvalidToken;

            _logger.LogWarning("FCM returned {StatusCode} for a push.", (int)response.StatusCode);
            return PushResult.Failed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sending a push notification failed.");
            return PushResult.Failed;
        }
    }

    // 404 / UNREGISTERED: o token não existe mais. 400 só conta quando o erro é sobre o token em si
    // (um 400 por payload inválido não pode apagar o token do usuário).
    private static bool IsUnknownToken(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound || body.Contains("UNREGISTERED", StringComparison.Ordinal))
            return true;

        return status == HttpStatusCode.BadRequest &&
               body.Contains("INVALID_ARGUMENT", StringComparison.Ordinal) &&
               body.Contains("registration token", StringComparison.OrdinalIgnoreCase);
    }
}
