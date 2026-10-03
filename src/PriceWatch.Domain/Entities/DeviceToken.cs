namespace PriceWatch.Domain.Entities;

/// <summary>
/// Token de push (FCM) de um dispositivo do usuário. O token é único: se o mesmo aparelho passar
/// a ser usado por outra conta, o registro muda de dono em vez de duplicar (e o alerta nunca vai
/// para a conta antiga). Dado pessoal: apagado com a conta.
/// </summary>
public class DeviceToken
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string Token { get; private set; } = default!;
    public string Platform { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    private DeviceToken() { }

    public static DeviceToken Create(string userId, string token, string platform)
    {
        var now = DateTime.UtcNow;
        return new DeviceToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Token = token,
            Platform = platform,
            CreatedAt = now,
            LastSeenAt = now
        };
    }

    public static DeviceToken Restore(
        string id, string userId, string token, string platform, DateTime createdAt, DateTime lastSeenAt) =>
        new()
        {
            Id = id, UserId = userId, Token = token, Platform = platform,
            CreatedAt = createdAt, LastSeenAt = lastSeenAt
        };

    public void Touch() => LastSeenAt = DateTime.UtcNow;

    public void ReassignTo(string userId)
    {
        UserId = userId;
        Touch();
    }
}
