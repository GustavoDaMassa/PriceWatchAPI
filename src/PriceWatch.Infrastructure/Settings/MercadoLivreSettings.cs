namespace PriceWatch.Infrastructure.Settings;

public class MercadoLivreSettings
{
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
