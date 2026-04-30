using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainNotification = PriceWatch.Domain.Entities.Notification;

namespace PriceWatch.UnitTests.Application.UseCases.Notification;

public class GetNotificationsUseCaseTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly GetNotificationsUseCase _useCase;

    public GetNotificationsUseCaseTests()
    {
        _useCase = new GetNotificationsUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_ShouldReturnNotificationsForUser()
    {
        var notifications = new List<DomainNotification>
        {
            DomainNotification.Create("user-1", "prod-1", "Prod A", NotificationType.TargetPriceReached, 90m),
            DomainNotification.Create("user-1", "prod-2", "Prod B", NotificationType.NewLowestPrice, 50m),
        };
        _repo.Setup(r => r.GetByUserIdAsync("user-1", null)).ReturnsAsync(notifications);

        var result = await _useCase.ExecuteAsync("user-1", null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_WithIsReadFilter_ShouldFilterCorrectly()
    {
        var unread = DomainNotification.Create("user-1", "prod-1", "Prod A", NotificationType.TargetPriceReached, 90m);
        _repo.Setup(r => r.GetByUserIdAsync("user-1", false)).ReturnsAsync(new[] { unread });

        var result = await _useCase.ExecuteAsync("user-1", false);

        result.Should().HaveCount(1);
        result.First().IsRead.Should().BeFalse();
    }
}
