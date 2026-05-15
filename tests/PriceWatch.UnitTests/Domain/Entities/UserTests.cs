using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Exceptions;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Create_ShouldSetPropertiesCorrectly()
    {
        var user = User.Create("Ana", "ana@test.com", "hashed_pw", "token123");

        user.Name.Should().Be("Ana");
        user.Email.Should().Be("ana@test.com");
        user.PasswordHash.Should().Be("hashed_pw");
        user.EmailVerificationToken.Should().Be("token123");
        user.IsEmailVerified.Should().BeFalse();
        user.Id.Should().NotBeNullOrEmpty();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var u1 = User.Create("A", "a@test.com", "h", "t");
        var u2 = User.Create("B", "b@test.com", "h", "t");

        u1.Id.Should().NotBe(u2.Id);
    }

    [Fact]
    public void Create_ShouldSetTokenExpiryInFuture()
    {
        var user = User.Create("A", "a@test.com", "h", "tok");

        user.TokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void VerifyEmail_WithValidToken_ShouldMarkEmailVerified()
    {
        var user = User.Create("A", "a@test.com", "h", "correct-token");

        user.VerifyEmail("correct-token");

        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.TokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public void VerifyEmail_WithWrongToken_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "h", "correct-token");

        var act = () => user.VerifyEmail("wrong-token");

        act.Should().Throw<BusinessException>().WithMessage("*Invalid*");
    }

    [Fact]
    public void VerifyEmail_WithExpiredToken_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "h", "token", tokenExpiresAt: DateTime.UtcNow.AddHours(-1));

        var act = () => user.VerifyEmail("token");

        act.Should().Throw<BusinessException>().WithMessage("*expired*");
    }

    [Fact]
    public void RegenerateVerificationToken_ShouldUpdateToken()
    {
        var user = User.Create("A", "a@test.com", "h", "old-token");

        user.RegenerateVerificationToken("new-token");

        user.EmailVerificationToken.Should().Be("new-token");
        user.TokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void RegenerateVerificationToken_WhenAlreadyVerified_ShouldThrowBusinessException()
    {
        var user = User.Create("A", "a@test.com", "h", "token");
        user.VerifyEmail("token");

        var act = () => user.RegenerateVerificationToken("new-token");

        act.Should().Throw<BusinessException>().WithMessage("*already verified*");
    }
}
