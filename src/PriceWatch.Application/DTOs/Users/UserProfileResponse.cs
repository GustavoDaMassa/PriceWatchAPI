namespace PriceWatch.Application.DTOs.Users;

public record UserProfileResponse(string Id, string Name, string Email, bool IsEmailVerified, DateTime CreatedAt);
