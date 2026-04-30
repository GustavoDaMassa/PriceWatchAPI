using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IPriceSnapshotRepository
{
    Task CreateAsync(PriceSnapshot snapshot);
    Task<IEnumerable<PriceSnapshot>> GetByProductIdAsync(string productId, int limit = 100);
}
