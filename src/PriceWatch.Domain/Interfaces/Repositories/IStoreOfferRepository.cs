using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IStoreOfferRepository
{
    Task<StoreOffer?> GetByIdAsync(string id);
    Task<StoreOffer?> GetBySourceAndUrlAsync(ProductSource source, string url);
    Task<IEnumerable<StoreOffer>> GetByProductIdAsync(string productId);
    Task<IEnumerable<StoreOffer>> GetDueForCheckAsync();
    Task CreateAsync(StoreOffer offer);
    Task UpdateAsync(StoreOffer offer);
}
