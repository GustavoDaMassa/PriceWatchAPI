using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IMongoCollection<ProductDocument> _collection;

    public ProductRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ProductDocument>("products");
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        var doc = await _collection.Find(d => d.Id == id).FirstOrDefaultAsync();
        return doc is null ? null : ProductMappings.ToDomain(doc);
    }

    public async Task<Product?> GetByGtinAsync(string gtin, string? excludeId = null)
    {
        var filter = excludeId is null
            ? Builders<ProductDocument>.Filter.Eq(d => d.Gtin, gtin)
            : Builders<ProductDocument>.Filter.And(
                Builders<ProductDocument>.Filter.Eq(d => d.Gtin, gtin),
                Builders<ProductDocument>.Filter.Ne(d => d.Id, excludeId));

        var doc = await _collection.Find(filter).FirstOrDefaultAsync();
        return doc is null ? null : ProductMappings.ToDomain(doc);
    }

    public async Task<IEnumerable<Product>> GetWithoutGtinAsync(string excludeId)
    {
        var docs = await _collection
            .Find(d => d.Gtin == null && d.Id != excludeId)
            .ToListAsync();
        return docs.Select(ProductMappings.ToDomain);
    }

    public async Task CreateAsync(Product product)
    {
        await _collection.InsertOneAsync(ProductMappings.ToDocument(product));
    }

    public async Task UpdateAsync(Product product)
    {
        await _collection.ReplaceOneAsync(d => d.Id == product.Id, ProductMappings.ToDocument(product));
    }
}
