using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Notification;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainNotification = PriceWatch.Domain.Entities.Notification;

namespace PriceWatch.UnitTests.Application.UseCases.Notification;

public class MarkAsReadUseCaseTests
{
    private readonly Mock<INotificationRepository> _repo = new();
    private readonly MarkAsReadUseCase _useCase;

    public MarkAsReadUseCaseTests()
    {
        _useCase = new MarkAsReadUseCase(_repo.Object);
    }

    [Fact]
    public async Task Execute_WithValidNotification_ShouldMarkAndUpdate()
    {
        var notif = DomainNotification.Create("user-1", "prod-1", "Prod A", NotificationType.TargetPriceReached, 90m);
        _repo.Setup(r => r.GetByIdAsync(notif.Id)).ReturnsAsync(notif);

        await _useCase.ExecuteAsync(notif.Id, "user-1");

        notif.IsRead.Should().BeTrue();
        _repo.Verify(r => r.UpdateAsync(It.Is<DomainNotification>(n => n.IsRead)), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenNotificationNotFound_ShouldThrowNotFoundException()
    {
        _repo.Setup(r => r.GetByIdAsync("non-existent")).ReturnsAsync((DomainNotification?)null);

        var act = async () => await _useCase.ExecuteAsync("non-existent", "user-1");

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }

    [Fact]
    public async Task Execute_WhenNotificationBelongsToOtherUser_ShouldThrowNotFoundException()
    {
        var notif = DomainNotification.Create("other-user", "prod-1", "Prod A", NotificationType.TargetPriceReached, 90m);
        _repo.Setup(r => r.GetByIdAsync(notif.Id)).ReturnsAsync(notif);

        var act = async () => await _useCase.ExecuteAsync(notif.Id, "user-1");

        await act.Should().ThrowAsync<NotificationNotFoundException>();
    }
}
