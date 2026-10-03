using FluentAssertions;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.ValueObjects;
using Xunit;

namespace PriceWatch.UnitTests.Domain.Entities;

public class OfferCandidateTests
{
    private static readonly OfferSearchResult Found =
        new("https://www.kabum.com.br/produto/1", "Placa RTX", 2599.90m, "https://img/a.png", "7891234567895", "Gigabyte", "RTX 4060");

    [Fact]
    public void Create_CopiesTheSearchResultAndSetsTheExpiration()
    {
        var candidate = OfferCandidate.Create("product-1", ProductSource.Kabum, Found, 0.91, CandidateReason.Keyword, TimeSpan.FromHours(24));

        candidate.Id.Should().NotBeNullOrEmpty();
        candidate.ProductId.Should().Be("product-1");
        candidate.Source.Should().Be(ProductSource.Kabum);
        candidate.Url.Should().Be(Found.Url);
        candidate.Name.Should().Be("Placa RTX");
        candidate.Price.Should().Be(2599.90m);
        candidate.Gtin.Should().Be("7891234567895");
        candidate.Score.Should().Be(0.91);
        candidate.Reason.Should().Be(CandidateReason.Keyword);
        candidate.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void IsExpired_IsTrueOnlyAfterTheExpirationInstant()
    {
        var fresh = OfferCandidate.Create("p", ProductSource.Kabum, Found, 1, CandidateReason.Gtin, TimeSpan.FromHours(1));
        var stale = OfferCandidate.Create("p", ProductSource.Kabum, Found, 1, CandidateReason.Gtin, TimeSpan.FromSeconds(-1));

        fresh.IsExpired().Should().BeFalse();
        stale.IsExpired().Should().BeTrue();
    }
}

public class OfferMatchFeedbackTests
{
    [Fact]
    public void Create_RecordsTheVerdictAndWhetherTheUserWantsToTrack()
    {
        var feedback = OfferMatchFeedback.Create("user-1", "candidate-1", "product-1", "https://loja/p/1", MatchVerdict.Same, track: true);

        feedback.UserId.Should().Be("user-1");
        feedback.CandidateId.Should().Be("candidate-1");
        feedback.ProductId.Should().Be("product-1");
        feedback.Url.Should().Be("https://loja/p/1");
        feedback.Verdict.Should().Be(MatchVerdict.Same);
        feedback.Track.Should().BeTrue();
        feedback.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNotSame_NeverTracks()
    {
        var feedback = OfferMatchFeedback.Create("u", "c", "p", "url", MatchVerdict.NotSame, track: true);

        feedback.Track.Should().BeFalse();
    }
}
