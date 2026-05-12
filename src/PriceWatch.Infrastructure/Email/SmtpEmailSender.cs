using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Infrastructure.Settings;

namespace PriceWatch.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly EmailTemplateRenderer _renderer = new();

    public SmtpEmailSender(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendVerificationEmailAsync(string email, string name, string token)
    {
        var verificationUrl =
            $"{_settings.FrontendBaseUrl.TrimEnd('/')}/verify-email" +
            $"?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

        var html = _renderer.Render("verification", new()
        {
            ["name"] = name,
            ["verificationUrl"] = verificationUrl,
        });

        await SendAsync(email, "Verify your PriceWatch account", html);
    }

    public async Task SendAlertEmailAsync(string email, string subject, string body)
    {
        // Strip "PriceWatch: " prefix from subject to use as heading inside the email body
        var heading = subject.StartsWith("PriceWatch: ")
            ? subject["PriceWatch: ".Length..]
            : subject;

        var html = _renderer.Render("alert", new()
        {
            ["heading"] = heading,
            ["message"] = body,
            ["appUrl"] = _settings.FrontendBaseUrl.TrimEnd('/'),
        });

        await SendAsync(email, subject, html);
    }

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        var secure = _settings.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(_settings.Host, _settings.Port, secure);

        if (!string.IsNullOrEmpty(_settings.User) && !string.IsNullOrEmpty(_settings.Password))
            await client.AuthenticateAsync(_settings.User, _settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
