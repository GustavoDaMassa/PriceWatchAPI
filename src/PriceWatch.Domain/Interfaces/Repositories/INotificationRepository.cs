using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface INotificationRepository
{
    Task<IEnumerable<Notification>> GetByUserIdAsync(string userId, bool? isRead = null);
    Task<Notification?> GetByIdAsync(string id);
    Task CreateAsync(Notification notification);
    Task UpdateAsync(Notification notification);
    Task DeleteByUserIdAsync(string userId);
}
