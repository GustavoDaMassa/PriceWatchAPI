using PriceWatch.Domain.Interfaces.Services;

namespace PriceWatch.IntegrationTests.Fixtures;

public class FakeEmailSender : IEmailSender
{
    private readonly List<(string Email, string Name, string Token)> _verifications = new();

    public Task SendVerificationEmailAsync(string email, string name, string token)
    {
        _verifications.Add((email, name, token));
        return Task.CompletedTask;
    }

    public Task SendAlertEmailAsync(string email, string subject, string body)
        => Task.CompletedTask;

    public string? GetVerificationToken(string email) =>
        _verifications.LastOrDefault(e => e.Email == email).Token;
}
