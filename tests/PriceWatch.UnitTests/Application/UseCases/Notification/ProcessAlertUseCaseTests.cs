using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainNotification = PriceWatch.Domain.Entities.Notification;

namespace PriceWatch.UnitTests.Application.UseCases.Notification;

public class ProcessAlertUseCaseTests
{
    private readonly Mock<INotificationRepository> _notificationRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IDeviceTokenRepository> _devices = new();
    private readonly Mock<IPushSender> _push = new();
    private readonly ProcessAlertUseCase _useCase;

    public ProcessAlertUseCaseTests()
    {
        _push.Setup(p => p.IsAvailable).Returns(true);
        _push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushMessage>())).ReturnsAsync(PushResult.Sent);
        _devices.Setup(r => r.GetByUserIdAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<DeviceToken>());
        _useCase = new ProcessAlertUseCase(_notificationRepo.Object, _userRepo.Object, _emailSender.Object, _devices.Object, _push.Object);
    }

    private static User BuildUser(bool isEmailVerified)
    {
        var user = User.Create("Test User", "user@test.com", "hash", "token-abc");
        if (isEmailVerified)
            user.VerifyEmail(user.EmailVerificationToken!);
        return user;
    }

    [Fact]
    public async Task Execute_AlwaysCreatesNotification()
    {
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync((User?)null);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _notificationRepo.Verify(r => r.CreateAsync(
            It.Is<DomainNotification>(n => n.UserId == "user-1" && n.ProductId == "prod-1")),
            Times.Once);
    }

    [Fact]
    public async Task Execute_UserNotFound_DoesNotSendEmail()
    {
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync((User?)null);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UserNotVerified_DoesNotSendEmail()
    {
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(BuildUser(isEmailVerified: false));

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UserVerified_SendsEmailToUserEmail()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Execute_TargetPriceReached_UsesCorrectSubject()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("target price")),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_NewLowestPrice_UsesCorrectSubject()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.NewLowestPrice, 75m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("lowest price")),
            It.IsAny<string>(),
            It.IsAny<string>()),
            Times.Once);
    }

    private static DeviceToken Device(string userId, string token) => DeviceToken.Create(userId, token, "android");

    private User UserWithDevices(bool verified, string locale, params DeviceToken[] devices)
    {
        var user = User.Create("Test User", "user@test.com", "hash", "token-abc", locale);
        if (verified) user.VerifyEmail(user.EmailVerificationToken!);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _devices.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(devices);
        return user;
    }

    [Fact]
    public async Task Execute_SendsAPushToEveryDeviceOfTheUserInTheirLanguage()
    {
        var user = UserWithDevices(verified: false, locale: "pt-BR", Device("u", "token-1"), Device("u", "token-2"));

        await _useCase.ExecuteAsync(user.Id, "item-1", "Notebook Acer", NotificationType.TargetPriceReached, 2599.9m);

        _push.Verify(p => p.SendAsync("token-1", It.Is<PushMessage>(m =>
            m.Title == "Preço-alvo atingido" && m.Body == "Notebook Acer: R$ 2.599,90" && m.Data!["productId"] == "item-1")), Times.Once);
        _push.Verify(p => p.SendAsync("token-2", It.IsAny<PushMessage>()), Times.Once);
    }

    [Fact]
    public async Task Execute_PushDoesNotDependOnTheEmailBeingVerified()
    {
        var user = UserWithDevices(verified: false, locale: "en", Device("u", "token-1"));

        await _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.NewLowestPrice, 99m);

        _push.Verify(p => p.SendAsync("token-1", It.IsAny<PushMessage>()), Times.Once);
        _emailSender.Verify(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_DiscardsTokensTheProviderNoLongerRecognizes()
    {
        var stale = Device("u", "stale-token");
        var good = Device("u", "good-token");
        var user = UserWithDevices(verified: true, locale: "pt-BR", stale, good);
        _push.Setup(p => p.SendAsync("stale-token", It.IsAny<PushMessage>())).ReturnsAsync(PushResult.InvalidToken);

        await _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        _devices.Verify(r => r.DeleteAsync(stale.Id), Times.Once);
        _devices.Verify(r => r.DeleteAsync(good.Id), Times.Never);
    }

    [Fact]
    public async Task Execute_ATransientPushFailure_KeepsTheToken()
    {
        var device = Device("u", "token-1");
        var user = UserWithDevices(verified: true, locale: "pt-BR", device);
        _push.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushMessage>())).ReturnsAsync(PushResult.Failed);

        await _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        _devices.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WhenPushIsNotConfigured_DoesNotLoadDevicesOrSend()
    {
        _push.Setup(p => p.IsAvailable).Returns(false);
        var user = UserWithDevices(verified: true, locale: "pt-BR", Device("u", "token-1"));

        await _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        _devices.Verify(r => r.GetByUserIdAsync(It.IsAny<string>()), Times.Never);
        _push.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushMessage>()), Times.Never);
    }

    [Fact]
    public async Task Execute_AFailureLoadingDevices_DoesNotBreakTheNotificationOrTheEmail()
    {
        var user = UserWithDevices(verified: true, locale: "pt-BR");
        _devices.Setup(r => r.GetByUserIdAsync(user.Id)).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        await act.Should().NotThrowAsync();
        _notificationRepo.Verify(r => r.CreateAsync(It.IsAny<DomainNotification>()), Times.Once);
        _emailSender.Verify(e => e.SendAlertEmailAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>(), "pt-BR"), Times.Once);
    }

    [Fact]
    public async Task Execute_PushGoesOutBeforeTheEmail_SoAMailServerFailureDoesNotCostThePush()
    {
        var user = UserWithDevices(verified: true, locale: "pt-BR", Device("u", "token-1"));
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var act = () => _useCase.ExecuteAsync(user.Id, "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _push.Verify(p => p.SendAsync("token-1", It.IsAny<PushMessage>()), Times.Once);
    }

    [Fact]
    public async Task Execute_UserNotFound_DoesNotPush()
    {
        _userRepo.Setup(r => r.GetByIdAsync("ghost")).ReturnsAsync((User?)null);

        await _useCase.ExecuteAsync("ghost", "item-1", "Mouse", NotificationType.TargetPriceReached, 99m);

        _push.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<PushMessage>()), Times.Never);
    }
}
