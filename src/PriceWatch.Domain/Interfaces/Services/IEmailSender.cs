namespace PriceWatch.Domain.Interfaces.Services;

public interface IEmailSender
{
    Task SendVerificationEmailAsync(string email, string name, string token, string locale = "en");
    Task SendAlertEmailAsync(string email, string subject, string body, string locale = "en");
}
