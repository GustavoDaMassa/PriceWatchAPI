using PriceWatch.Domain.Entities;

namespace PriceWatch.Domain.Interfaces.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
