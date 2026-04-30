using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainNotification = PriceWatch.Domain.Entities.Notification;

namespace PriceWatch.UnitTests.Application.UseCases.Notification;

public class ProcessAlertUseCaseTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly ProcessAlertUseCase _useCase;

    public ProcessAlertUseCaseTests()
    {
        _useCase = new ProcessAlertUseCase(_repo.Object, _emailSender.Object);
    }

    [Fact]
    public async Task Execute_ShouldCreateNotificationAndSendEmail()
    {
        string? capturedEmail = null;
        _emailSender.Setup(e => e.SendAlertEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string>((email, subject, body) => capturedEmail = email)
            .Returns(Task.CompletedTask);

        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.TargetPriceReached, 89m, "user@test.com");

        _repo.Verify(r => r.CreateAsync(It.IsAny<DomainNotification>()), Times.Once);
        _emailSender.Verify(e => e.SendAlertEmailAsync("user@test.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ShouldCallRepositoryCreate()
    {
        await _useCase.ExecuteAsync("user-1", "prod-1", "Produto X", NotificationType.NewLowestPrice, 75m, "user@test.com");

        _repo.Verify(r => r.CreateAsync(
            It.Is<DomainNotification>(n => n.UserId == "user-1" && n.ProductId == "prod-1")),
            Times.Once);
    }
}
