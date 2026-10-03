using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.TrackedProduct;
using PriceWatch.Application.UseCases.Catalog;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Gerenciamento de produtos monitorados.</summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductCatalogFacade _catalog;

    public ProductsController(IProductCatalogFacade catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Retorna todos os produtos monitorados do usuário, opcionalmente filtrados por lista.</summary>
    /// <param name="listId">ID da lista (opcional). Se omitido, retorna todos os produtos.</param>
    /// <response code="200">Lista de produtos.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrackedProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts([FromQuery] string? listId = null)
    {
        var result = await _catalog.GetUserProductsAsync(GetUserId(), listId);
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
        var result = await _catalog.AddProductAsync(GetUserId(), request);
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
        await _catalog.UpdateProductAsync(id, GetUserId(), request);
        return NoContent();
    }

    /// <summary>Atribui ou remove o produto de uma lista.</summary>
    /// <param name="id">ID do produto.</param>
    /// <remarks>Envie listId com o ID da lista para atribuir, ou null para remover da lista atual.</remarks>
    /// <response code="204">Atualizado com sucesso.</response>
    /// <response code="404">Produto ou lista não encontrados.</response>
    [HttpPatch("{id}/list")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignToList(string id, [FromBody] AssignToListRequest request)
    {
        await _catalog.AssignToListAsync(id, GetUserId(), request);
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
        await _catalog.RemoveProductAsync(id, GetUserId());
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
        var result = await _catalog.GetPriceHistoryAsync(id, GetUserId());
        return Ok(result);
    }

    /// <summary>Registra o clique e devolve a URL da loja (link de afiliado, se houver) em JSON.</summary>
    /// <remarks>
    /// Os clientes (SPA, extensão, app) chamam este endpoint em vez de abrir a URL da loja direto, para o
    /// clique ser registrado e o link de afiliado ser aplicado. Num produto com várias lojas, abre a mais barata.
    /// </remarks>
    /// <param name="id">ID do produto.</param>
    /// <response code="200">URL de destino.</response>
    /// <response code="404">Produto não encontrado ou não pertence ao usuário.</response>
    [HttpPost("{id}/click")]
    [ProducesResponseType(typeof(OfferClickResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClickProduct(string id)
    {
        var url = await _catalog.OpenProductAsync(id, GetUserId());
        return Ok(new OfferClickResponse(url));
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
