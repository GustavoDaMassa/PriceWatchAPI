namespace PriceWatch.Infrastructure.Settings;

public class GeminiSettings
{
    public string? ApiKey { get; set; }

    /// <summary>Modelo do plano grátis da API Gemini. Configurável porque a lista de modelos grátis muda.</summary>
    public string Model { get; set; } = "gemini-3.1-flash-lite";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
