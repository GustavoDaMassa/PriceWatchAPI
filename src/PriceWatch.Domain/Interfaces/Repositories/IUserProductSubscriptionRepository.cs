using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IUserProductSubscriptionRepository
{
    Task<UserProductSubscription?> GetByIdAsync(string id);
    Task<UserProductSubscription?> GetByUserIdAndProductIdAsync(string userId, string productId);
    Task<IEnumerable<UserProductSubscription>> GetByUserIdAsync(string userId, string? listId = null);
    Task<IEnumerable<UserProductSubscription>> GetByProductIdAsync(string productId);
    Task CreateAsync(UserProductSubscription subscription);
    Task UpdateAsync(UserProductSubscription subscription);
    Task DeleteAsync(string id);
    Task DeleteByUserIdAsync(string userId);
    Task UnlinkByListIdAsync(string listId);
}
