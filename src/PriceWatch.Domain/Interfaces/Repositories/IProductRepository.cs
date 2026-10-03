using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<Product?> GetByGtinAsync(string gtin, string? excludeId = null);
    Task<IEnumerable<Product>> GetWithoutGtinAsync(string excludeId);
    Task CreateAsync(Product product);
    Task UpdateAsync(Product product);
}
