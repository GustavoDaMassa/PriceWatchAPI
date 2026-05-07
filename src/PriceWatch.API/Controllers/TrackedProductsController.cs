using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Gerenciamento de produtos monitorados dentro de uma lista.</summary>
[ApiController]
[Route("api/lists/{listId}/products")]
[Authorize]
[Produces("application/json")]
public class TrackedProductsController : ControllerBase
{
    private readonly GetProductsByListUseCase _getProducts;
    private readonly AddProductUseCase _addProduct;

    public TrackedProductsController(
        GetProductsByListUseCase getProducts,
        AddProductUseCase addProduct)
    {
        _getProducts = getProducts;
        _addProduct = addProduct;
    }

    /// <summary>Retorna todos os produtos monitorados em uma lista.</summary>
    /// <param name="listId">ID da lista.</param>
    /// <response code="200">Lista de produtos com preço atual, menor preço e próxima verificação.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrackedProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(string listId)
    {
        var result = await _getProducts.ExecuteAsync(listId);
        return Ok(result);
    }

    /// <summary>Adiciona um produto para monitoramento.</summary>
    /// <remarks>
    /// O sistema detecta a origem automaticamente pela URL e busca o nome e preço inicial via API.
    /// O preço é verificado automaticamente a cada 1 hora.
    /// </remarks>
    /// <param name="listId">ID da lista onde o produto será adicionado.</param>
    /// <response code="201">Produto adicionado e monitoramento iniciado.</response>
    /// <response code="400">URL não suportada ou produto não encontrado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TrackedProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddProduct(string listId, [FromBody] AddProductRequest request)
    {
        var result = await _addProduct.ExecuteAsync(GetUserId(), listId, request);
        return StatusCode(201, result);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
