namespace PriceWatch.Infrastructure.Settings;

public class RateLimitingSettings
{
    public int AnonymousPermitLimit { get; set; } = 30;
    public int AuthenticatedPermitLimit { get; set; } = 120;
    public int WindowSeconds { get; set; } = 60;
}
