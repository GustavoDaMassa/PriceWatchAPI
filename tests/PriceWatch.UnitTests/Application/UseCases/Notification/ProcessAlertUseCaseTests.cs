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
    private readonly ProcessAlertUseCase _useCase;

    public ProcessAlertUseCaseTests()
    {
        _useCase = new ProcessAlertUseCase(_notificationRepo.Object, _userRepo.Object, _emailSender.Object);
    }

    private static User BuildUser(bool isEmailVerified)
    {
        var user = User.Create("Test User", "user@test.com", "hash");
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

        _emailSender.Verify(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UserNotVerified_DoesNotSendEmail()
    {
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(BuildUser(isEmailVerified: false));

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UserVerified_SendsEmailToUserEmail()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Execute_TargetPriceReached_UsesCorrectSubject()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("target price")),
            It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Execute_NewLowestPrice_UsesCorrectSubject()
    {
        var user = BuildUser(isEmailVerified: true);
        _userRepo.Setup(r => r.GetByIdAsync("user-1")).ReturnsAsync(user);
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.NewLowestPrice, 75m);

        _emailSender.Verify(e => e.SendAlertEmailAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("lowest price")),
            It.IsAny<string>()),
            Times.Once);
    }
}
