namespace PriceWatch.Infrastructure.Fetchers;

public interface IMercadoLivreTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}
