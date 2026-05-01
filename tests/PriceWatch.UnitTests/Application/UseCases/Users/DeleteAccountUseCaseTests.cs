using FluentAssertions;
using Moq;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Exceptions;
using PriceWatch.Domain.Interfaces.Repositories;
using PriceWatch.Domain.Interfaces.Services;
using Xunit;
using DomainProductList = PriceWatch.Domain.Entities.ProductList;
using DomainTrackedProduct = PriceWatch.Domain.Entities.TrackedProduct;
using DomainUser = PriceWatch.Domain.Entities.User;

namespace PriceWatch.UnitTests.Application.UseCases.Users;

public class DeleteAccountUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IProductListRepository> _listRepo = new();
    private readonly Mock<ITrackedProductRepository> _productRepo = new();
    private readonly Mock<IPriceSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<INotificationRepository> _notifRepo = new();
    private readonly DeleteAccountUseCase _useCase;

    public DeleteAccountUseCaseTests()
    {
        _useCase = new DeleteAccountUseCase(
            _userRepo.Object,
            _hasher.Object,
            _listRepo.Object,
            _productRepo.Object,
            _snapshotRepo.Object,
            _notifRepo.Object);
    }

    [Fact]
    public async Task Execute_WithValidPassword_ShouldDeleteAllUserData()
    {
        var user = DomainUser.Create("A", "a@test.com", "hash", "tok");
        var list = DomainProductList.Create(user.Id, "Lista", null);
        var product = DomainTrackedProduct.Create(list.Id, user.Id, "url",
            PriceWatch.Domain.Enums.ProductSource.Manual, "Prod", 100m);

        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Pass123", "hash")).Returns(true);
        _listRepo.Setup(r => r.GetByUserIdAsync(user.Id))
            .ReturnsAsync(new List<DomainProductList> { list });
        _productRepo.Setup(r => r.GetByListIdAsync(list.Id))
            .ReturnsAsync(new List<DomainTrackedProduct> { product });

        await _useCase.ExecuteAsync(user.Id, "Pass123");

        _snapshotRepo.Verify(r => r.DeleteByProductIdAsync(product.Id), Times.Once);
        _productRepo.Verify(r => r.DeleteByListIdAsync(list.Id), Times.Once);
        _listRepo.Verify(r => r.DeleteByUserIdAsync(user.Id), Times.Once);
        _notifRepo.Verify(r => r.DeleteByUserIdAsync(user.Id), Times.Once);
        _userRepo.Verify(r => r.DeleteAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task Execute_WithWrongPassword_ShouldThrowBusinessException()
    {
        var user = DomainUser.Create("A", "a@test.com", "hash", "tok");
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "hash")).Returns(false);

        var act = async () => await _useCase.ExecuteAsync(user.Id, "wrong");

        await act.Should().ThrowAsync<BusinessException>().WithMessage("*incorrect*");
    }
}
