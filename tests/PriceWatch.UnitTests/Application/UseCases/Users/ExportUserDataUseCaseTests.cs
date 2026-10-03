using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using Xunit;
using DomainUser = PriceWatch.Domain.Entities.User;
using DomainList = PriceWatch.Domain.Entities.ProductList;
using DomainNotification = PriceWatch.Domain.Entities.Notification;

namespace PriceWatch.UnitTests.Application.UseCases.Users;

public class ExportUserDataUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IProductListRepository> _listRepo = new();
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptionRepo = new();
    private readonly Mock<INotificationRepository> _notificationRepo = new();
    private readonly Mock<IClickLogRepository> _clickRepo = new();
    private readonly Mock<IOfferMatchFeedbackRepository> _feedbackRepo = new();
    private readonly Mock<IDeviceTokenRepository> _deviceRepo = new();
    private readonly ExportUserDataUseCase _useCase;

    public ExportUserDataUseCaseTests()
    {
        _useCase = new ExportUserDataUseCase(
            _userRepo.Object, _listRepo.Object, _productRepo.Object, _subscriptionRepo.Object,
            _notificationRepo.Object, _clickRepo.Object, _feedbackRepo.Object, _deviceRepo.Object);
    }

    [Fact]
    public async Task Execute_WhenUserDoesNotExist_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync("u1")).ReturnsAsync((DomainUser?)null);

        var act = () => _useCase.ExecuteAsync("u1");

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Execute_ReturnsEveryCategoryOfUserData()
    {
        var user = DomainUser.Create("Ana", "ana@test.com", "secret-hash", "verification-token");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _listRepo.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(new[] { DomainList.Create(user.Id, "Setup", null) });
        _subscriptionRepo.Setup(r => r.GetByUserIdAsync(user.Id, null)).ReturnsAsync(new[] { UserProductSubscription.Create(user.Id, "p1", 99m) });
        _notificationRepo.Setup(r => r.GetByUserIdAsync(user.Id, null)).ReturnsAsync(new[]
        {
            DomainNotification.Create(user.Id, "p1", "Placa", NotificationType.TargetPriceReached, 99m),
        });
        _clickRepo.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(new[] { ClickLog.Create("o1", user.Id) });
        _feedbackRepo.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(new[]
        {
            OfferMatchFeedback.Create(user.Id, "c1", "p1", "https://loja/x", MatchVerdict.Same, true),
        });
        _deviceRepo.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(new[] { DeviceToken.Create(user.Id, "fcm-secret-token", "android") });

        var export = await _useCase.ExecuteAsync(user.Id);

        export.Profile.Email.Should().Be("ana@test.com");
        export.Lists.Should().ContainSingle(l => l.Name == "Setup");
        export.Subscriptions.Should().ContainSingle(s => s.ProductId == "p1" && s.TargetPrice == 99m);
        export.Notifications.Should().ContainSingle(n => n.ProductName == "Placa");
        export.Clicks.Should().ContainSingle(c => c.OfferId == "o1");
        export.MatchFeedback.Should().ContainSingle(f => f.Url == "https://loja/x" && f.IsSameProduct);
        export.Devices.Should().ContainSingle(d => d.Platform == "android");
        export.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Execute_NeverExposesPasswordHashVerificationTokenOrPushToken()
    {
        var user = DomainUser.Create("Ana", "ana@test.com", "secret-hash", "verification-token");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _deviceRepo.Setup(r => r.GetByUserIdAsync(user.Id)).ReturnsAsync(new[] { DeviceToken.Create(user.Id, "fcm-secret-token", "android") });

        var export = await _useCase.ExecuteAsync(user.Id);

        var json = System.Text.Json.JsonSerializer.Serialize(export);
        json.Should().NotContain("secret-hash").And.NotContain("verification-token").And.NotContain("fcm-secret-token");
    }
}
