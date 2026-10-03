using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace PriceWatch.IntegrationTests.Fixtures;

/// <summary>
/// Mesma aplicação com <c>Features:UnifiedCatalogEnabled=true</c>: os endpoints legados passam a
/// ler/escrever no catálogo novo (Product/StoreOffer/Subscription). Tem containers próprios.
/// </summary>
public class UnifiedPriceWatchWebApplicationFactory : PriceWatchWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:UnifiedCatalogEnabled"] = "true"
            });
        });
    }
}
