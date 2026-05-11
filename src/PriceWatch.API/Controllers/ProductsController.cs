using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Gerenciamento de produtos monitorados.</summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly GetUserProductsUseCase _getProducts;
    private readonly AddProductUseCase _addProduct;
    private readonly UpdateProductUseCase _updateProduct;
    private readonly RemoveProductUseCase _removeProduct;
    private readonly GetPriceHistoryUseCase _getPriceHistory;

    public ProductsController(
        GetUserProductsUseCase getProducts,
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

    /// <summary>Retorna todos os produtos monitorados do usuário, opcionalmente filtrados por lista.</summary>
    /// <param name="listId">ID da lista (opcional). Se omitido, retorna todos os produtos.</param>
    /// <response code="200">Lista de produtos.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrackedProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts([FromQuery] string? listId = null)
    {
        var result = await _getProducts.ExecuteAsync(GetUserId(), listId);
        return Ok(result);
    }

    /// <summary>Adiciona um produto para monitoramento.</summary>
    /// <remarks>
    /// O sistema detecta a origem automaticamente pela URL e busca o nome e preço inicial via API.
    /// O preço é verificado automaticamente a cada 1 hora.
    /// Informe listId no body para associar o produto a uma lista (opcional).
    /// </remarks>
    /// <response code="201">Produto adicionado e monitoramento iniciado.</response>
    /// <response code="400">URL não suportada ou produto não encontrado.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TrackedProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddProduct([FromBody] AddProductRequest request)
    {
        var result = await _addProduct.ExecuteAsync(GetUserId(), request);
        return StatusCode(201, result);
    }

    /// <summary>Atualiza o preço-alvo ou ativa/desativa o monitoramento de um produto.</summary>
    /// <param name="id">ID do produto.</param>
    /// <response code="204">Atualizado com sucesso.</response>
    /// <response code="404">Produto não encontrado ou não pertence ao usuário.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] UpdateProductRequest request)
    {
        await _updateProduct.ExecuteAsync(id, GetUserId(), request);
        return NoContent();
    }

    /// <summary>Remove um produto do monitoramento.</summary>
    /// <param name="id">ID do produto.</param>
    /// <response code="204">Removido com sucesso.</response>
    /// <response code="404">Produto não encontrado ou não pertence ao usuário.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProduct(string id)
    {
        await _removeProduct.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    /// <summary>Retorna o histórico de preços de um produto.</summary>
    /// <param name="id">ID do produto.</param>
    /// <response code="200">Histórico de snapshots de preço.</response>
    /// <response code="404">Produto não encontrado.</response>
    [HttpGet("{id}/history")]
    [ProducesResponseType(typeof(IEnumerable<PriceSnapshotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPriceHistory(string id)
    {
        var result = await _getPriceHistory.ExecuteAsync(id, GetUserId());
        return Ok(result);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
