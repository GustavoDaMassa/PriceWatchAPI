using PriceWatch.Domain.Exceptions;

namespace PriceWatch.Domain.Entities;

public class User
{
    public string Id { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string Locale { get; private set; } = "en";
    public bool IsEmailVerified { get; private set; }
    public string? EmailVerificationToken { get; private set; }
    public DateTime? TokenExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static User Restore(
        string id,
        string name,
        string email,
        string passwordHash,
        string locale,
        bool isEmailVerified,
        string? emailVerificationToken,
        DateTime? tokenExpiresAt,
        DateTime createdAt)
    {
        return new User
        {
            Id = id,
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Locale = locale,
            IsEmailVerified = isEmailVerified,
            EmailVerificationToken = emailVerificationToken,
            TokenExpiresAt = tokenExpiresAt,
            CreatedAt = createdAt
        };
    }

    public static User Create(
        string name,
        string email,
        string passwordHash,
        string verificationToken,
        string locale = "en",
        DateTime? tokenExpiresAt = null)
    {
        return new User
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Email = email,
            PasswordHash = passwordHash,
            Locale = locale,
            IsEmailVerified = false,
            EmailVerificationToken = verificationToken,
            TokenExpiresAt = tokenExpiresAt ?? DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void VerifyEmail(string token)
    {
        if (EmailVerificationToken != token)
            throw new BusinessException("Invalid verification token.");
        if (TokenExpiresAt < DateTime.UtcNow)
            throw new BusinessException("Verification token has expired.");

        IsEmailVerified = true;
        EmailVerificationToken = null;
        TokenExpiresAt = null;
    }

    public void RegenerateVerificationToken(string newToken, DateTime? expiresAt = null)
    {
        if (IsEmailVerified)
            throw new BusinessException("Email is already verified.");

        EmailVerificationToken = newToken;
        TokenExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(24);
    }

    public void UpdatePasswordHash(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }

    public void ChangeEmail(string newEmail, string verificationToken)
    {
        Email = newEmail;
        IsEmailVerified = false;
        EmailVerificationToken = verificationToken;
        TokenExpiresAt = DateTime.UtcNow.AddHours(24);
    }
}
