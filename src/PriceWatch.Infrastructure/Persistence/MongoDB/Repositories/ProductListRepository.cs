using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class ProductListRepository : IProductListRepository
{
    private readonly IMongoCollection<ProductListDocument> _collection;

    public ProductListRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ProductListDocument>("product_lists");
    }

    public async Task<IEnumerable<ProductList>> GetByUserIdAsync(string userId)
    {
        var docs = await _collection.Find(d => d.UserId == userId).ToListAsync();
        return docs.Select(ProductListMappings.ToDomain);
    }

    public async Task<ProductList?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ProductListMappings.ToDomain(doc);
    }

    public async Task CreateAsync(ProductList list)
    {
        await _collection.InsertOneAsync(ProductListMappings.ToDocument(list));
    }

    public async Task UpdateAsync(ProductList list)
    {
        await _collection.ReplaceOneAsync(d => d.Id == list.Id, ProductListMappings.ToDocument(list));
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(d => d.Id == id);
    }
}
