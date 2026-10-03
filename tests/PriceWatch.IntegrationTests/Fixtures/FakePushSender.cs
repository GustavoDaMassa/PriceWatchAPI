using System.Collections.Concurrent;
using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.IntegrationTests.Fixtures;

/// <summary>Remetente de push falso: guarda o que foi enviado e permite simular token inválido.</summary>
public class FakePushSender : IPushSender
{
    private readonly ConcurrentBag<(string Token, PushMessage Message)> _sent = new();
    private readonly ConcurrentDictionary<string, PushResult> _resultByToken = new();

    public bool IsAvailable => true;

    public IReadOnlyCollection<(string Token, PushMessage Message)> Sent => _sent;

    public void SetResult(string token, PushResult result) => _resultByToken[token] = result;

    public Task<PushResult> SendAsync(string deviceToken, PushMessage message)
    {
        _sent.Add((deviceToken, message));
        return Task.FromResult(_resultByToken.GetValueOrDefault(deviceToken, PushResult.Sent));
    }
}
