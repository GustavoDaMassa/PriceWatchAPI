using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Application.UseCases.TrackedProduct;

namespace PriceWatch.Application.UseCases.Catalog;

/// <summary>Modelo atual (TrackedProduct): só delega aos casos de uso existentes, sem mudar comportamento.</summary>
public class LegacyProductCatalogFacade : IProductCatalogFacade
{
    private readonly GetUserProductsUseCase _getProducts;
    private readonly AddProductUseCase _addProduct;
    private readonly UpdateProductUseCase _updateProduct;
    private readonly AssignProductToListUseCase _assignToList;
    private readonly RemoveProductUseCase _removeProduct;
    private readonly GetPriceHistoryUseCase _getPriceHistory;
    private readonly GetListAnalysisUseCase _getListAnalysis;

    public LegacyProductCatalogFacade(
        GetUserProductsUseCase getProducts,
        AddProductUseCase addProduct,
        UpdateProductUseCase updateProduct,
        AssignProductToListUseCase assignToList,
        RemoveProductUseCase removeProduct,
        GetPriceHistoryUseCase getPriceHistory,
        GetListAnalysisUseCase getListAnalysis)
    {
        _getProducts = getProducts;
        _addProduct = addProduct;
        _updateProduct = updateProduct;
        _assignToList = assignToList;
        _removeProduct = removeProduct;
        _getPriceHistory = getPriceHistory;
        _getListAnalysis = getListAnalysis;
    }

    public Task<IEnumerable<TrackedProductResponse>> GetUserProductsAsync(string userId, string? listId) =>
        _getProducts.ExecuteAsync(userId, listId);

    public Task<TrackedProductResponse> AddProductAsync(string userId, AddProductRequest request) =>
        _addProduct.ExecuteAsync(userId, request);

    public Task UpdateProductAsync(string productId, string userId, UpdateProductRequest request) =>
        _updateProduct.ExecuteAsync(productId, userId, request);

    public Task AssignToListAsync(string productId, string userId, AssignToListRequest request) =>
        _assignToList.ExecuteAsync(productId, userId, request);

    public Task RemoveProductAsync(string productId, string userId) =>
        _removeProduct.ExecuteAsync(productId, userId);

    public Task<IEnumerable<PriceSnapshotResponse>> GetPriceHistoryAsync(string productId, string userId) =>
        _getPriceHistory.ExecuteAsync(productId, userId);

    public Task<IEnumerable<AnalysisItemDto>> GetListAnalysisAsync(string listId, string userId) =>
        _getListAnalysis.ExecuteAsync(listId, userId);

    // modelo legado: não há ofertas nem rede de afiliados, então a saída é a própria URL do item
    public async Task<string> OpenProductAsync(string productId, string userId)
    {
        var items = await _getProducts.ExecuteAsync(userId);
        return items.FirstOrDefault(i => i.Id == productId)?.Url
            ?? throw new Domain.Exceptions.TrackedProductNotFoundException(productId);
    }
}
