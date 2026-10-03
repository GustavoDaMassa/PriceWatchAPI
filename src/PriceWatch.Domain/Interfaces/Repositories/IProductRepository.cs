using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<IEnumerable<Product>> GetByIdsAsync(IEnumerable<string> ids);
    Task<Product?> GetByGtinAsync(string gtin, string? excludeId = null);
    Task<IEnumerable<Product>> GetWithoutGtinAsync(string excludeId);
    Task CreateAsync(Product product);
    Task UpdateAsync(Product product);
}
