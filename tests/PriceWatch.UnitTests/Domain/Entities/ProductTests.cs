using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class ProductTests
{
    [Fact]
    public void Create_ShouldStartAsUnmatched()
    {
        var product = Product.Create("iPhone 15 128GB", gtin: "00190199719729", brand: "Apple", model: "A3092");

        product.MatchStatus.Should().Be(MatchStatus.Unmatched);
        product.Id.Should().NotBeNullOrEmpty();
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAutoMatched_ShouldTransitionStatus()
    {
        var product = Product.Create("iPhone 15 128GB");

        product.MarkAutoMatched();

        product.MatchStatus.Should().Be(MatchStatus.AutoMatched);
    }

    [Fact]
    public void SuggestMatch_ShouldTransitionToPendingReviewAndStoreCandidate()
    {
        var product = Product.Create("iPhone 15 128GB");

        product.SuggestMatch("candidate-product-1");

        product.MatchStatus.Should().Be(MatchStatus.PendingReview);
        product.SuggestedMatchProductId.Should().Be("candidate-product-1");
    }

    [Theory]
    [InlineData(MatchStatus.Unmatched, true)]
    [InlineData(MatchStatus.AutoMatched, true)]
    [InlineData(MatchStatus.Confirmed, true)]
    [InlineData(MatchStatus.PendingReview, false)]
    [InlineData(MatchStatus.Rejected, false)]
    public void IsVisibleInComparison_DependsOnMatchStatus(MatchStatus status, bool expected)
    {
        var product = Product.Create("iPhone 15 128GB");

        switch (status)
        {
            case MatchStatus.AutoMatched: product.MarkAutoMatched(); break;
            case MatchStatus.PendingReview: product.SuggestMatch("candidate-product-1"); break;
            case MatchStatus.Confirmed: product.Confirm(); break;
            case MatchStatus.Rejected: product.Reject(); break;
        }

        product.IsVisibleInComparison().Should().Be(expected);
    }
}
