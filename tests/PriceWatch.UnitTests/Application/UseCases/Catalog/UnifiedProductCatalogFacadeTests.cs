using FluentAssertions;
using Moq;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.Interfaces;
using PriceWatch.Application.UseCases.Catalog;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Entities;
using PriceWatch.Domain.Enums;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;

namespace PriceWatch.UnitTests.Application.UseCases.Catalog;

public class UnifiedProductCatalogFacadeTests
{
    private readonly Mock<IUserProductSubscriptionRepository> _subscriptions = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IStoreOfferRepository> _offers = new();
    private readonly Mock<IPriceSnapshotRepository> _snapshots = new();
    private readonly Mock<IProductListRepository> _lists = new();
    private readonly Mock<IPriceFetcherResolver> _fetchers = new();
    private readonly Mock<IPriceFetcher> _fetcher = new();
    private readonly Mock<IClickLogRepository> _clicks = new();
    private readonly Mock<IAffiliateLinkResolver> _affiliateResolver = new();
    private readonly Mock<IAffiliateLinkGenerator> _affiliateGenerator = new();
    private readonly UnifiedProductCatalogFacade _facade;

    private const string UserId = "user-1";
    private const string Url = "https://produto.mercadolivre.com.br/p/MLB1";

    private readonly Product _product = Product.Create("Notebook Gamer", imageUrl: "https://img/n.png");

    public UnifiedProductCatalogFacadeTests()
    {
        _fetchers.Setup(r => r.Resolve(It.IsAny<string>())).Returns(_fetcher.Object);
        _fetcher.Setup(f => f.ProductSource).Returns(ProductSource.MercadoLivre);

        var sync = new SyncOfferActivityUseCase(_offers.Object, _subscriptions.Object);
        var createOffer = new CreateOfferUseCase(
            _products.Object, _offers.Object, _subscriptions.Object, _fetchers.Object,
            new Mock<IProductMatchingEventPublisher>().Object, sync, new Mock<IAffiliateLinkResolver>().Object);

        _affiliateResolver.Setup(r => r.Resolve(It.IsAny<ProductSource>())).Returns(_affiliateGenerator.Object);
        _affiliateGenerator.Setup(g => g.GenerateAsync(It.IsAny<string>())).ReturnsAsync((string url) => url);

        _facade = new UnifiedProductCatalogFacade(
            _subscriptions.Object, _products.Object, _offers.Object, _snapshots.Object,
            _lists.Object, _fetchers.Object, createOffer, sync,
            new RedirectOfferUseCase(_offers.Object, _affiliateResolver.Object, _clicks.Object));

        _products.Setup(r => r.GetByIdAsync(_product.Id)).ReturnsAsync(_product);
        _products.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(new[] { _product });
        _subscriptions.Setup(r => r.GetByProductIdAsync(It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<UserProductSubscription>());
        _offers.Setup(r => r.GetByProductIdAsync(It.IsAny<string>())).ReturnsAsync(Array.Empty<StoreOffer>());
    }

    private StoreOffer Offer(decimal price, decimal lowest, string url, ProductSource source = ProductSource.MercadoLivre, bool active = true)
    {
        var offer = StoreOffer.Create(_product.Id, source, url);
        offer.RecordPrice(price);
        offer.RestoreLowestPrice(lowest);
        if (!active) offer.Deactivate();
        return offer;
    }

    private UserProductSubscription Subscription(decimal target = 100m, string? listId = null, bool active = true)
    {
        var sub = UserProductSubscription.Create(UserId, _product.Id, target, listId);
        if (!active) sub.Deactivate();
        _subscriptions.Setup(r => r.GetByIdAsync(sub.Id)).ReturnsAsync(sub);
        return sub;
    }

    private void OffersAre(params StoreOffer[] offers)
    {
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(offers);
        _offers.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(offers);
    }

    [Fact]
    public async Task GetUserProducts_MapsTheSubscriptionAndItsCheapestActiveOffer()
    {
        var sub = Subscription(target: 900m, listId: "list-1");
        var expensive = Offer(1200m, 1100m, "https://ml/a");
        var cheap = Offer(1000m, 950m, "https://kabum/b", ProductSource.Kabum);
        var inactiveCheaper = Offer(500m, 500m, "https://other/c", active: false);
        OffersAre(expensive, cheap, inactiveCheaper);
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, null)).ReturnsAsync(new[] { sub });

        var items = (await _facade.GetUserProductsAsync(UserId, null)).ToList();

        var item = items.Should().ContainSingle().Subject;
        item.Id.Should().Be(sub.Id);
        item.ProductId.Should().Be(_product.Id);
        item.ListId.Should().Be("list-1");
        item.Name.Should().Be("Notebook Gamer");
        item.ImageUrl.Should().Be("https://img/n.png");
        item.Url.Should().Be("https://kabum/b");
        item.Source.Should().Be(ProductSource.Kabum);
        item.TargetPrice.Should().Be(900m);
        item.CurrentPrice.Should().Be(1000m);
        item.LowestPrice.Should().Be(500m);
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserProducts_ListsTheMostRecentSubscriptionFirst()
    {
        OffersAre(Offer(1000m, 950m, "https://ml/a"));
        var now = DateTime.UtcNow;
        var oldest = UserProductSubscription.Restore("sub-old", UserId, _product.Id, null, 100m, true, false, now.AddDays(-2));
        var newest = UserProductSubscription.Restore("sub-new", UserId, _product.Id, null, 100m, true, false, now);
        var middle = UserProductSubscription.Restore("sub-mid", UserId, _product.Id, null, 100m, true, false, now.AddDays(-1));
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, null)).ReturnsAsync(new[] { oldest, newest, middle });

        var items = (await _facade.GetUserProductsAsync(UserId, null)).ToList();

        items.Select(i => i.Id).Should().Equal("sub-new", "sub-mid", "sub-old");
    }

    [Fact]
    public async Task GetUserProducts_SkipsSubscriptionsWhoseProductHasNoOffers()
    {
        var sub = Subscription();
        OffersAre();
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, null)).ReturnsAsync(new[] { sub });

        var items = await _facade.GetUserProductsAsync(UserId, null);

        items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProduct_WhenTheUserAlreadyTracksIt_ReturnsTheExistingItemKeepingItsTarget()
    {
        var offer = Offer(1000m, 1000m, Url);
        var sub = Subscription(target: 800m);
        OffersAre(offer);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, Url)).ReturnsAsync(offer);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, _product.Id)).ReturnsAsync(sub);

        var result = await _facade.AddProductAsync(UserId, new AddProductRequest(Url, 0m));

        result.Id.Should().Be(sub.Id);
        result.TargetPrice.Should().Be(800m);
        _subscriptions.Verify(r => r.UpdateAsync(It.IsAny<UserProductSubscription>()), Times.Never);
        _subscriptions.Verify(r => r.CreateAsync(It.IsAny<UserProductSubscription>()), Times.Never);
    }

    [Fact]
    public async Task AddProduct_WhenTheExistingItemIsPaused_ReactivatesItAndItsOffers()
    {
        var offer = Offer(1000m, 1000m, Url, active: false);
        var sub = Subscription(target: 800m, active: false);
        OffersAre(offer);
        _offers.Setup(r => r.GetBySourceAndUrlAsync(ProductSource.MercadoLivre, Url)).ReturnsAsync(offer);
        _subscriptions.Setup(r => r.GetByUserIdAndProductIdAsync(UserId, _product.Id)).ReturnsAsync(sub);
        _subscriptions.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { sub });

        var result = await _facade.AddProductAsync(UserId, new AddProductRequest(Url, 0m));

        result.IsActive.Should().BeTrue();
        sub.IsActive.Should().BeTrue();
        offer.IsActive.Should().BeTrue();
        _subscriptions.Verify(r => r.UpdateAsync(sub), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_ChangesTargetAndActiveFlagThenSyncsOffers()
    {
        var offer = Offer(1000m, 1000m, Url);
        var sub = Subscription(target: 800m);
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { offer });
        _subscriptions.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { sub });

        await _facade.UpdateProductAsync(sub.Id, UserId, new UpdateProductRequest(700m, false));

        sub.TargetPrice.Should().Be(700m);
        sub.IsActive.Should().BeFalse();
        offer.IsActive.Should().BeFalse();
        _subscriptions.Verify(r => r.UpdateAsync(sub), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_OfAnotherUser_ThrowsNotFound()
    {
        var sub = Subscription();

        var act = () => _facade.UpdateProductAsync(sub.Id, "someone-else", new UpdateProductRequest(1m, true));

        await act.Should().ThrowAsync<TrackedProductNotFoundException>();
    }

    [Fact]
    public async Task RemoveProduct_DeletesTheSubscriptionAndPausesOffersWithoutSubscribers()
    {
        var offer = Offer(1000m, 1000m, Url);
        var sub = Subscription();
        _offers.Setup(r => r.GetByProductIdAsync(_product.Id)).ReturnsAsync(new[] { offer });

        await _facade.RemoveProductAsync(sub.Id, UserId);

        _subscriptions.Verify(r => r.DeleteAsync(sub.Id), Times.Once);
        offer.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveProduct_OfAnotherUser_ThrowsNotFoundAndKeepsTheSubscription()
    {
        var sub = Subscription();

        var act = () => _facade.RemoveProductAsync(sub.Id, "someone-else");

        await act.Should().ThrowAsync<TrackedProductNotFoundException>();
        _subscriptions.Verify(r => r.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignToList_ChecksListOwnershipAndMovesTheItem()
    {
        var sub = Subscription();
        var list = DomainProductList.Create(UserId, "Setup", null);
        _lists.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);

        await _facade.AssignToListAsync(sub.Id, UserId, new AssignToListRequest(list.Id));

        sub.ListId.Should().Be(list.Id);
        _subscriptions.Verify(r => r.UpdateAsync(sub), Times.Once);
    }

    [Fact]
    public async Task AssignToList_WithNullListId_RemovesTheItemFromItsList()
    {
        var sub = Subscription(listId: "list-1");

        await _facade.AssignToListAsync(sub.Id, UserId, new AssignToListRequest(null));

        sub.ListId.Should().BeNull();
    }

    [Fact]
    public async Task AssignToList_WithAListOfAnotherUser_ThrowsListNotFound()
    {
        var sub = Subscription();
        var foreignList = DomainProductList.Create("someone-else", "Deles", null);
        _lists.Setup(r => r.GetByIdAsync(foreignList.Id)).ReturnsAsync(foreignList);

        var act = () => _facade.AssignToListAsync(sub.Id, UserId, new AssignToListRequest(foreignList.Id));

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }

    [Fact]
    public async Task GetPriceHistory_ReadsTheSnapshotsOfThePrimaryOffer()
    {
        var sub = Subscription();
        var cheap = Offer(900m, 900m, "https://kabum/b", ProductSource.Kabum);
        var expensive = Offer(1200m, 1200m, "https://ml/a");
        OffersAre(expensive, cheap);
        var snapshot = PriceSnapshot.Create(cheap.Id, 900m);
        _snapshots.Setup(r => r.GetByProductIdAsync(cheap.Id, It.IsAny<int>())).ReturnsAsync(new[] { snapshot });

        var history = (await _facade.GetPriceHistoryAsync(sub.Id, UserId)).ToList();

        history.Should().ContainSingle().Which.Price.Should().Be(900m);
    }

    [Fact]
    public async Task GetPriceHistory_OfAnotherUser_ThrowsNotFound()
    {
        var sub = Subscription();

        var act = () => _facade.GetPriceHistoryAsync(sub.Id, "someone-else");

        await act.Should().ThrowAsync<TrackedProductNotFoundException>();
    }

    [Fact]
    public async Task GetListAnalysis_ReturnsActiveItemsOrderedByDistanceToTarget()
    {
        var list = DomainProductList.Create(UserId, "Analise", null);
        _lists.Setup(r => r.GetByIdAsync(list.Id)).ReturnsAsync(list);
        var near = Subscription(target: 190m, listId: list.Id);
        var far = Subscription(target: 100m, listId: list.Id);
        var paused = Subscription(target: 150m, listId: list.Id, active: false);
        OffersAre(Offer(199.90m, 199.90m, Url));
        _subscriptions.Setup(r => r.GetByUserIdAsync(UserId, list.Id)).ReturnsAsync(new[] { far, paused, near });

        var analysis = (await _facade.GetListAnalysisAsync(list.Id, UserId)).ToList();

        analysis.Select(a => a.ProductId).Should().Equal(near.Id, far.Id);
        analysis[0].DistancePercent.Should().Be(5.21m);
        analysis[0].ProductName.Should().Be("Notebook Gamer");
    }

    [Fact]
    public async Task GetListAnalysis_OfAnotherUsersList_ThrowsListNotFound()
    {
        var foreignList = DomainProductList.Create("someone-else", "Deles", null);
        _lists.Setup(r => r.GetByIdAsync(foreignList.Id)).ReturnsAsync(foreignList);

        var act = () => _facade.GetListAnalysisAsync(foreignList.Id, UserId);

        await act.Should().ThrowAsync<ProductListNotFoundException>();
    }

    [Fact]
    public async Task OpenProduct_ClicksTheCheapestActiveOfferAndReturnsItsLink()
    {
        var sub = Subscription();
        var expensive = Offer(1200m, 1100m, "https://ml/a");
        var cheap = Offer(1000m, 950m, "https://kabum/b", ProductSource.Kabum);
        cheap.SetAffiliateUrl("https://afiliado/?u=kabum");
        OffersAre(expensive, cheap);
        _offers.Setup(r => r.GetByIdAsync(cheap.Id)).ReturnsAsync(cheap);

        var url = await _facade.OpenProductAsync(sub.Id, UserId);

        url.Should().Be("https://afiliado/?u=kabum");
        _clicks.Verify(r => r.CreateAsync(It.Is<ClickLog>(c => c.OfferId == cheap.Id && c.UserId == UserId)), Times.Once);
    }

    [Fact]
    public async Task OpenProduct_OfAnotherUser_ThrowsNotFoundAndRecordsNoClick()
    {
        var sub = Subscription();

        var act = () => _facade.OpenProductAsync(sub.Id, "someone-else");

        await act.Should().ThrowAsync<TrackedProductNotFoundException>();
        _clicks.Verify(r => r.CreateAsync(It.IsAny<ClickLog>()), Times.Never);
    }
}
