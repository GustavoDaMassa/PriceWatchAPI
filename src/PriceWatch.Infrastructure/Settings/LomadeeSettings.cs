namespace PriceWatch.Infrastructure.Settings;

public class LomadeeSettings
{
    public string? AppToken { get; set; }
    public string? SourceId { get; set; }
    public string BaseUrl { get; set; } = "https://api.lomadee.com/v2";
}
