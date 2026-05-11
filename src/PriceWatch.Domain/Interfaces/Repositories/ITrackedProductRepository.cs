using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface ITrackedProductRepository
{
    Task<IEnumerable<TrackedProduct>> GetByUserIdAsync(string userId, string? listId = null);
    Task<TrackedProduct?> GetByIdAsync(string id);
    Task<IEnumerable<TrackedProduct>> GetDueForCheckAsync();
    Task CreateAsync(TrackedProduct product);
    Task UpdateAsync(TrackedProduct product);
    Task DeleteAsync(string id);
    Task UnlinkByListIdAsync(string listId);
}
