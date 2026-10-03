using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using PriceWatch.Domain.Services;

namespace PriceWatch.Application.UseCases.Notification;

public class ProcessAlertUseCase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly IDeviceTokenRepository _deviceTokenRepository;
    private readonly IPushSender _pushSender;

    public ProcessAlertUseCase(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IDeviceTokenRepository deviceTokenRepository,
        IPushSender pushSender)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _deviceTokenRepository = deviceTokenRepository;
        _pushSender = pushSender;
    }

    public async Task ExecuteAsync(
        string userId,
        string productId,
        string productName,
        NotificationType type,
        decimal currentPrice)
    {
        var notification = Domain.Entities.Notification.Create(userId, productId, productName, type, currentPrice);
        await _notificationRepository.CreateAsync(notification);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return;

        // push antes do e-mail: o e-mail é o único canal que pode lançar (SMTP) e o consumer não
        // reprocessa mensagens com erro, então uma falha de SMTP não pode custar o push
        await TrySendPushAsync(user, productId, productName, type, currentPrice, notification.Id);

        if (!user.IsEmailVerified)
            return;

        var subject = type == NotificationType.TargetPriceReached
            ? $"PriceWatch: '{productName}' reached your target price!"
            : $"PriceWatch: New lowest price for '{productName}'!";

        await _emailSender.SendAlertEmailAsync(user.Email, subject, notification.Message, user.Locale);
    }

    // best-effort: nenhuma falha de push pode impedir a notificação in-app nem o e-mail. O
    // IPushSender não lança e registra os próprios erros; aqui só se protege o acesso ao banco.
    private async Task TrySendPushAsync(
        Domain.Entities.User user,
        string productId,
        string productName,
        NotificationType type,
        decimal currentPrice,
        string notificationId)
    {
        if (!_pushSender.IsAvailable)
            return;

        try
        {
            var devices = (await _deviceTokenRepository.GetByUserIdAsync(user.Id)).ToList();
            if (devices.Count == 0)
                return;

            var text = AlertTexts.Push(user.Locale, type, productName, currentPrice);
            var message = text with
            {
                Data = new Dictionary<string, string>(text.Data!)
                {
                    ["productId"] = productId,
                    ["notificationId"] = notificationId
                }
            };

            foreach (var device in devices)
            {
                var result = await _pushSender.SendAsync(device.Token, message);
                if (result == PushResult.InvalidToken)
                    await _deviceTokenRepository.DeleteAsync(device.Id); // app desinstalado ou token expirado
            }
        }
        catch
        {
            // ver comentário acima
        }
    }
}
