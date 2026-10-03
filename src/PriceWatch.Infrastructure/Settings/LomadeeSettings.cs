namespace PriceWatch.Infrastructure.Settings;

public class LomadeeSettings
{
    public string? AppToken { get; set; }
    public string? SourceId { get; set; }
    public string BaseUrl { get; set; } = "https://api.lomadee.com/v2";

    // sem credenciais a integração fica desligada (CanHandle = false) em vez de falhar em runtime
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppToken) && !string.IsNullOrWhiteSpace(SourceId);
}
