using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

[ApiController]
[Route("api/lists/{listId}/products")]
[Authorize]
public class TrackedProductsController : ControllerBase
{
    private readonly GetProductsByListUseCase _getProducts;
    private readonly AddProductUseCase _addProduct;
    private readonly UpdateProductUseCase _updateProduct;
    private readonly RemoveProductUseCase _removeProduct;
    private readonly GetPriceHistoryUseCase _getPriceHistory;

    public TrackedProductsController(
        GetProductsByListUseCase getProducts,
        AddProductUseCase addProduct,
        UpdateProductUseCase updateProduct,
        RemoveProductUseCase removeProduct,
        GetPriceHistoryUseCase getPriceHistory)
    {
        _getProducts = getProducts;
        _addProduct = addProduct;
        _updateProduct = updateProduct;
        _removeProduct = removeProduct;
        _getPriceHistory = getPriceHistory;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(string listId)
    {
        var result = await _getProducts.ExecuteAsync(listId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> AddProduct(string listId, [FromBody] AddProductRequest request)
    {
        var result = await _addProduct.ExecuteAsync(GetUserId(), request);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(string listId, string id, [FromBody] UpdateProductRequest request)
    {
        await _updateProduct.ExecuteAsync(id, GetUserId(), request);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveProduct(string listId, string id)
    {
        await _removeProduct.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetPriceHistory(string listId, string id)
    {
        var result = await _getPriceHistory.ExecuteAsync(id, GetUserId());
        return Ok(result);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
