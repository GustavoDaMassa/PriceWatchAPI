using PriceWatch.Domain.Entities;
using PriceWatch.Infrastructure.Persistence.MongoDB.Documents;

namespace PriceWatch.Infrastructure.Persistence.MongoDB.Mappings;

public static class UserMappings
{
    public static UserDocument ToDocument(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        PasswordHash = user.PasswordHash,
        IsEmailVerified = user.IsEmailVerified,
        EmailVerificationToken = user.EmailVerificationToken,
        TokenExpiresAt = user.TokenExpiresAt,
        CreatedAt = user.CreatedAt
    };

    public static User ToDomain(UserDocument doc) => User.Restore(
        doc.Id,
        doc.Name,
        doc.Email,
        doc.PasswordHash,
        doc.IsEmailVerified,
        doc.EmailVerificationToken,
        doc.TokenExpiresAt,
        doc.CreatedAt);
}
