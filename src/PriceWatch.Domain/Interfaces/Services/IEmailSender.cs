namespace PriceWatch.Domain.Interfaces.Services;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string name, string token);
}
