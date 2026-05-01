using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class UserManagementTests
{
    [Fact]
    public void UpdatePasswordHash_ShouldReplaceHash()
    {
        var user = User.Create("A", "a@test.com", "old-hash", "tok");
        user.VerifyEmail("tok");

        user.UpdatePasswordHash("new-hash");

        user.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public void ChangeEmail_ShouldUpdateEmailAndMarkUnverified()
    {
        var user = User.Create("A", "old@test.com", "hash", "tok");
        user.VerifyEmail("tok");
        user.IsEmailVerified.Should().BeTrue();

        user.ChangeEmail("new@test.com", "new-token");

        user.Email.Should().Be("new@test.com");
        user.IsEmailVerified.Should().BeFalse();
        user.EmailVerificationToken.Should().Be("new-token");
        user.TokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }
}
