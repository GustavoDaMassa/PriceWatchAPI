using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByIdAsync(string id);
    Task<DeviceToken?> GetByTokenAsync(string token);
    Task<IEnumerable<DeviceToken>> GetByUserIdAsync(string userId);

    /// <summary>Insere ou atualiza pelo id.</summary>
    Task SaveAsync(DeviceToken device);

    Task DeleteAsync(string id);
    Task DeleteByUserIdAsync(string userId);
}
