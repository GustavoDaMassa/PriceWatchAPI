using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.Infrastructure.Fetchers;

public class MercadoLivreFetcher : IPriceFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;

    public string Source => "mercadolivre";

    public MercadoLivreFetcher(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<decimal> FetchAsync(string url)
    {
        // TODO: implement Mercado Livre API integration
        await Task.CompletedTask;
        return 0m;
    }
}
