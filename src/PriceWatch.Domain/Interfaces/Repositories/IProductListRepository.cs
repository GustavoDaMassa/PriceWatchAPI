using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Repositories;

public interface IProductListRepository
{
    Task<IEnumerable<ProductList>> GetByUserIdAsync(string userId);
    Task<ProductList?> GetByIdAsync(string id);
    Task CreateAsync(ProductList list);
    Task UpdateAsync(ProductList list);
    Task DeleteAsync(string id);
    Task DeleteByUserIdAsync(string userId);
}
