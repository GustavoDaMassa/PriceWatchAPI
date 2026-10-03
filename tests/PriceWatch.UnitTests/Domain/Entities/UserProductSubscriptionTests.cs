using FluentAssertions;
using PriceWatch.Domain.Entities;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class UserProductSubscriptionTests
{
    private static UserProductSubscription CreateSubscription(decimal targetPrice = 100m) =>
        UserProductSubscription.Create("user-1", "product-1", targetPrice, "list-1");

    [Fact]
    public void Create_ShouldSetInitialValues_IsActiveTrue()
    {
        var subscription = CreateSubscription();

        subscription.UserId.Should().Be("user-1");
        subscription.ProductId.Should().Be("product-1");
        subscription.ListId.Should().Be("list-1");
        subscription.IsActive.Should().BeTrue();
        subscription.TargetAlertSent.Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerTargetAlert_WhenAtOrBelowTarget_ReturnsTrue()
    {
        var subscription = CreateSubscription(targetPrice: 100m);

        subscription.ShouldTriggerTargetAlert(99m).Should().BeTrue();
    }

    [Fact]
    public void ShouldTriggerTargetAlert_WhenAboveTarget_ReturnsFalse()
    {
        var subscription = CreateSubscription(targetPrice: 100m);

        subscription.ShouldTriggerTargetAlert(101m).Should().BeFalse();
    }

    [Fact]
    public void ShouldTriggerTargetAlert_AfterAlreadySent_ReturnsFalse()
    {
        var subscription = CreateSubscription(targetPrice: 100m);
        subscription.MarkTargetAlertSent();

        subscription.ShouldTriggerTargetAlert(99m).Should().BeFalse();
    }

    [Fact]
    public void SetTargetPrice_ShouldResetAlertSentFlag()
    {
        var subscription = CreateSubscription(targetPrice: 100m);
        subscription.MarkTargetAlertSent();

        subscription.SetTargetPrice(50m);

        subscription.TargetPrice.Should().Be(50m);
        subscription.TargetAlertSent.Should().BeFalse();
    }

    [Fact]
    public void RemoveFromList_ShouldClearListId()
    {
        var subscription = CreateSubscription();

        subscription.RemoveFromList();

        subscription.ListId.Should().BeNull();
    }

    [Fact]
    public void ReassignToProduct_ShouldChangeProductId()
    {
        var subscription = CreateSubscription();

        subscription.ReassignToProduct("product-2");

        subscription.ProductId.Should().Be("product-2");
    }

    [Fact]
    public void Create_WithExplicitId_ShouldUseIt()
    {
        var subscription = UserProductSubscription.Create("user-1", "product-1", 100m, id: "legacy-id");

        subscription.Id.Should().Be("legacy-id");
    }

    [Theory]
    [InlineData(100, 120, false)]
    [InlineData(100, 100, true)]
    [InlineData(100, 80, true)]
    [InlineData(0, 120, true)]
    public void ResetTargetAlertIfAbove_ShouldOnlyResetWhenPriceIsAboveAPositiveTarget(
        decimal target, decimal price, bool expectedStillSent)
    {
        var subscription = CreateSubscription(target);
        subscription.MarkTargetAlertSent();

        subscription.ResetTargetAlertIfAbove(price);

        subscription.TargetAlertSent.Should().Be(expectedStillSent);
    }
}
