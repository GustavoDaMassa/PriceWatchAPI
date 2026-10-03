using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IClickLogRepository
{
    Task CreateAsync(ClickLog click);
    Task<long> CountByOfferIdAsync(string offerId);
    Task<IEnumerable<ClickLog>> GetByUserIdAsync(string userId);
    Task DeleteByUserIdAsync(string userId);
}
