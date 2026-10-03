using System.Text;

namespace PriceWatch.Infrastructure.Settings;

/// <summary>
/// Firebase Cloud Messaging (HTTP v1). Credenciais SEMPRE por variável de ambiente, nunca no
/// repositório: <c>Fcm__ProjectId</c> e a conta de serviço em <c>Fcm__ServiceAccountJson</c> ou, mais
/// fácil de colocar num .env de uma linha só, <c>Fcm__ServiceAccountJsonBase64</c>.
/// </summary>
public class FcmSettings
{
    public string? ProjectId { get; set; }
    public string? ServiceAccountJson { get; set; }
    public string? ServiceAccountJsonBase64 { get; set; }

    public string? ResolveServiceAccountJson()
    {
        if (!string.IsNullOrWhiteSpace(ServiceAccountJson))
            return ServiceAccountJson;

        return string.IsNullOrWhiteSpace(ServiceAccountJsonBase64)
            ? null
            : Encoding.UTF8.GetString(Convert.FromBase64String(ServiceAccountJsonBase64));
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId) &&
        (!string.IsNullOrWhiteSpace(ServiceAccountJson) || !string.IsNullOrWhiteSpace(ServiceAccountJsonBase64));
}
