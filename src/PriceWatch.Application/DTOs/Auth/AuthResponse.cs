namespace PriceWatch.Application.DTOs.Auth;

public record AuthResponse(string Token, string Email, string Name, bool IsEmailVerified);
