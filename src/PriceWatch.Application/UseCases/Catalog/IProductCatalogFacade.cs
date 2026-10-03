using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.DTOs.TrackedProduct;

namespace PriceWatch.Application.UseCases.Catalog;

/// <summary>
/// Operações dos endpoints legados (/api/products e análise de lista) com contrato fixo. Duas
/// implementações: <see cref="LegacyProductCatalogFacade"/> (modelo TrackedProduct) e
/// <see cref="UnifiedProductCatalogFacade"/> (catálogo Product/StoreOffer/Subscription). A escolha é
/// feita por <c>Features:UnifiedCatalogEnabled</c>, o que permite o cutover (e o rollback) sem
/// mexer em controllers nem quebrar a SPA e a extensão já publicadas.
/// </summary>
public interface IProductCatalogFacade
{
    Task<IEnumerable<TrackedProductResponse>> GetUserProductsAsync(string userId, string? listId);
    Task<TrackedProductResponse> AddProductAsync(string userId, AddProductRequest request);
    Task UpdateProductAsync(string productId, string userId, UpdateProductRequest request);
    Task AssignToListAsync(string productId, string userId, AssignToListRequest request);
    Task RemoveProductAsync(string productId, string userId);
    Task<IEnumerable<PriceSnapshotResponse>> GetPriceHistoryAsync(string productId, string userId);
    Task<IEnumerable<AnalysisItemDto>> GetListAnalysisAsync(string listId, string userId);
}
