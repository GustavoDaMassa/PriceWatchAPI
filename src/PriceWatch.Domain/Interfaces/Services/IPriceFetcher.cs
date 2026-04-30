namespace PriceWatch.Domain.Interfaces.Services;

public interface IPriceFetcher
{
    string Source { get; }
    Task<decimal> FetchAsync(string url);
}
