using FluentAssertions;
using Moq;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class RedirectOfferUseCaseTests
{
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IAffiliateLinkResolver> _affiliateResolver = new();
    private readonly Mock<IAffiliateLinkGenerator> _generator = new();
    private readonly RedirectOfferUseCase _useCase;

    public RedirectOfferUseCaseTests()
    {
        _affiliateResolver.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_generator.Object);
        _useCase = new RedirectOfferUseCase(_offers.Object, _affiliateResolver.Object);
    }

    [Fact]
    public async Task Execute_WhenOfferDoesNotExist_ShouldThrowStoreOfferNotFoundException()
    {
        _offers.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((StoreOffer?)null);

        var act = () => _useCase.ExecuteAsync("missing");

        await act.Should().ThrowAsync<StoreOfferNotFoundException>();
    }

    [Fact]
    public async Task Execute_WhenAffiliateUrlNotCached_ShouldGenerateAndPersistIt()
    {
        var offer = StoreOffer.Create("product-1", ProductSource.MercadoLivre, "https://ml.com/item");
        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);
        _generator.Setup(g => g.GenerateAsync(offer.Url)).ReturnsAsync("https://ml.com/item?affiliate=abc");

        var result = await _useCase.ExecuteAsync(offer.Id);

        result.AffiliateUrl.Should().Be("https://ml.com/item?affiliate=abc");
        _offers.Verify(r => r.UpdateAsync(It.Is<StoreOffer>(o => o.AffiliateUrl == "https://ml.com/item?affiliate=abc")), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenAffiliateUrlAlreadyCached_ShouldReuseItWithoutRegenerating()
    {
        var offer = StoreOffer.Create("product-1", ProductSource.MercadoLivre, "https://ml.com/item");
        offer.SetAffiliateUrl("https://ml.com/item?affiliate=cached");
        _offers.Setup(r => r.GetByIdAsync(offer.Id)).ReturnsAsync(offer);

        var result = await _useCase.ExecuteAsync(offer.Id);

        result.AffiliateUrl.Should().Be("https://ml.com/item?affiliate=cached");
        _generator.Verify(g => g.GenerateAsync(It.IsAny<string>()), Times.Never);
        _offers.Verify(r => r.UpdateAsync(It.IsAny<StoreOffer>()), Times.Never);
    }
}
