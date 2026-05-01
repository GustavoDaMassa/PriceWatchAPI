using MongoDB.Driver;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;
using PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument> _collection;

    public UserRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<UserDocument>("users");
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var doc = await _collection
            .Find(u => u.Email == email)
            .FirstOrDefaultAsync();
        return doc is null ? null : UserMappings.ToDomain(doc);
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        var doc = await _collection
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync();
        return doc is null ? null : UserMappings.ToDomain(doc);
    }

    public async Task CreateAsync(User user)
    {
        await _collection.InsertOneAsync(UserMappings.ToDocument(user));
    }

    public async Task UpdateAsync(User user)
    {
        await _collection.ReplaceOneAsync(
            u => u.Id == user.Id,
            UserMappings.ToDocument(user));
    }

    public async Task DeleteAsync(string id)
    {
        await _collection.DeleteOneAsync(u => u.Id == id);
    }
}
