namespace PriceWatch.Infrastructure.Settings;

public class SmtpSettings
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = 1025;
    public string From { get; set; } = default!;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
}
