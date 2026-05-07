using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.TrackedProduct;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Operações em produtos monitorados individuais.</summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly UpdateProductUseCase _updateProduct;
    private readonly RemoveProductUseCase _removeProduct;
    private readonly GetPriceHistoryUseCase _getPriceHistory;

    public ProductsController(
        UpdateProductUseCase updateProduct,
        RemoveProductUseCase removeProduct,
        GetPriceHistoryUseCase getPriceHistory)
    {
        _updateProduct = updateProduct;
        _removeProduct = removeProduct;
        _getPriceHistory = getPriceHistory;
    }

    /// <summary>Atualiza o preço-alvo ou ativa/desativa o monitoramento de um produto.</summary>
    /// <param name="id">ID do produto.</param>
    /// <param name="request">Novos valores de targetPrice e isActive.</param>
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
    /// <remarks>Cada entrada representa uma verificação de preço. Ordenado do mais recente para o mais antigo.</remarks>
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
