using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;

    public SmtpEmailSender(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendVerificationEmailAsync(string email, string name, string token)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Verify your PriceWatch account";
        message.Body = new TextPart("plain")
        {
            Text = $"Hello {name},\n\nYour verification token is: {token}\n\nUse it to verify your email."
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, false);

        if (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password))
            await client.AuthenticateAsync(_settings.User, _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendAlertEmailAsync(string email, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, false);

        if (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password))
            await client.AuthenticateAsync(_settings.User, _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
