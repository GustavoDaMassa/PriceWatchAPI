namespace PriceWatch.Domain.Interfaces.Services;

public enum PushResult
{
    Sent,

    /// <summary>O provedor não reconhece mais o token (app desinstalado, token expirado): deve ser descartado.</summary>
    InvalidToken,

    /// <summary>Falha transitória ou de configuração: o token continua válido.</summary>
    Failed
}

public record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

/// <summary>
/// Envia uma notificação push a um dispositivo. Contrato: <b>nunca lança</b> (uma falha de push não
/// pode impedir o alerta por e-mail nem a notificação in-app).
/// </summary>
public interface IPushSender
{
    /// <summary>False quando o provedor não está configurado (credenciais ausentes).</summary>
    bool IsAvailable => true;

    Task<PushResult> SendAsync(string deviceToken, PushMessage message);
}
