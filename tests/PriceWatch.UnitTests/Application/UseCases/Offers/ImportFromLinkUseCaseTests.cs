using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using Xunit;

namespace PriceWatch.UnitTests.Application.UseCases.Offers;

public class ImportFromLinkUseCaseTests
{
    private readonly Mock<IPriceFetcherResolver> _fetchers = new();
    private readonly Mock<IUrlResolver> _urls = new();
    private readonly Mock<ICreateOfferUseCase> _createOffer = new();
    private readonly ImportFromLinkUseCase _useCase;

    private static OfferResponse AnyOffer(string url) =>
        new("sub-1", "product-1", "offer-1", "Produto", url, null, ProductSource.MercadoLivre, 100m, 150m, 150m, true, DateTime.UtcNow);

    public ImportFromLinkUseCaseTests()
    {
        _createOffer.Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CreateOfferRequest>()))
            .ReturnsAsync((string _, CreateOfferRequest r) => AnyOffer(r.Url));
        _useCase = new ImportFromLinkUseCase(_fetchers.Object, _urls.Object, _createOffer.Object);
    }

    [Fact]
    public async Task Execute_WithASupportedUrl_NormalizesItAndDelegatesWithoutFollowingRedirects()
    {
        _fetchers.Setup(f => f.CanHandle("https://www.kabum.com.br/produto/1")).Returns(true);

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest("  https://WWW.kabum.com.br/produto/1?utm_source=zap#x ", 90m, "list-1"));

        _urls.Verify(u => u.ResolveFinalUrlAsync(It.IsAny<string>()), Times.Never);
        _createOffer.Verify(c => c.ExecuteAsync("user-1",
            It.Is<CreateOfferRequest>(r => r.Url == "https://www.kabum.com.br/produto/1" && r.TargetPrice == 90m && r.ListId == "list-1")), Times.Once);
    }

    [Fact]
    public async Task Execute_WithAShortLink_ResolvesItAndImportsTheFinalUrl()
    {
        _fetchers.Setup(f => f.CanHandle("https://mercadolivre.com/sec/abc")).Returns(false);
        _fetchers.Setup(f => f.CanHandle("https://www.mercadolivre.com.br/p/MLB123")).Returns(true);
        _urls.Setup(u => u.ResolveFinalUrlAsync("https://mercadolivre.com/sec/abc"))
            .ReturnsAsync("https://www.mercadolivre.com.br/p/MLB123?matt_tool=9#reviews");

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest("https://mercadolivre.com/sec/abc", 0m));

        _createOffer.Verify(c => c.ExecuteAsync("user-1",
            It.Is<CreateOfferRequest>(r => r.Url == "https://www.mercadolivre.com.br/p/MLB123")), Times.Once);
    }

    [Fact]
    public async Task Execute_WhenTheFinalUrlIsStillNotSupported_ThrowsAndImportsNothing()
    {
        _fetchers.Setup(f => f.CanHandle(It.IsAny<string>())).Returns(false);
        _urls.Setup(u => u.ResolveFinalUrlAsync(It.IsAny<string>())).ReturnsAsync("https://loja-desconhecida.com/p/1");

        var act = () => _useCase.ExecuteAsync("user-1", new CreateOfferRequest("https://curto.link/x", 0m));

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*not supported*");
        _createOffer.Verify(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CreateOfferRequest>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithAnInvalidUrl_ThrowsBeforeAnyNetworkAccess()
    {
        var act = () => _useCase.ExecuteAsync("user-1", new CreateOfferRequest("javascript:alert(1)", 0m));

        await act.Should().ThrowAsync<BusinessException>();
        _urls.Verify(u => u.ResolveFinalUrlAsync(It.IsAny<string>()), Times.Never);
        _createOffer.Verify(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CreateOfferRequest>()), Times.Never);
    }

    [Fact]
    public async Task Execute_PassesTheClientDataThrough()
    {
        _fetchers.Setup(f => f.CanHandle(It.IsAny<string>())).Returns(true);
        var clientData = new ClientProductData(Brand: "Acme", Model: "X1");

        await _useCase.ExecuteAsync("user-1", new CreateOfferRequest("https://loja.com.br/p/1", 0m, ClientData: clientData));

        _createOffer.Verify(c => c.ExecuteAsync("user-1",
            It.Is<CreateOfferRequest>(r => r.ClientData == clientData)), Times.Once);
    }
}
