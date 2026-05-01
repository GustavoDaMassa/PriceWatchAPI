using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Gerenciamento de listas de produtos monitorados.</summary>
[ApiController]
[Route("api/lists")]
[Authorize]
[Produces("application/json")]
public class ProductListsController : ControllerBase
{
    private readonly GetUserListsUseCase _getUserLists;
    private readonly CreateListUseCase _createList;
    private readonly UpdateListUseCase _updateList;
    private readonly DeleteListUseCase _deleteList;
    private readonly GetListAnalysisUseCase _getListAnalysis;

    public ProductListsController(
        GetUserListsUseCase getUserLists,
        CreateListUseCase createList,
        UpdateListUseCase updateList,
        DeleteListUseCase deleteList,
        GetListAnalysisUseCase getListAnalysis)
    {
        _getUserLists = getUserLists;
        _createList = createList;
        _updateList = updateList;
        _deleteList = deleteList;
        _getListAnalysis = getListAnalysis;
    }

    /// <summary>Retorna todas as listas do usuário autenticado.</summary>
    /// <response code="200">Lista de coleções do usuário.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserLists()
    {
        var result = await _getUserLists.ExecuteAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>Cria uma nova lista de produtos.</summary>
    /// <remarks>Exemplo de uso: "Black Friday", "Setup Gamer", "Eletrodomésticos".</remarks>
    /// <response code="201">Lista criada. Retorna o objeto completo com id.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateList([FromBody] CreateProductListRequest request)
    {
        var result = await _createList.ExecuteAsync(GetUserId(), request);
        return StatusCode(201, result);
    }

    /// <summary>Atualiza nome e/ou descrição de uma lista.</summary>
    /// <param name="id">ID da lista a atualizar.</param>
    /// <response code="204">Atualizado com sucesso.</response>
    /// <response code="404">Lista não encontrada ou não pertence ao usuário.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateList(string id, [FromBody] UpdateProductListRequest request)
    {
        await _updateList.ExecuteAsync(id, GetUserId(), request);
        return NoContent();
    }

    /// <summary>Remove uma lista e todos os produtos vinculados a ela.</summary>
    /// <param name="id">ID da lista a remover.</param>
    /// <response code="204">Removido com sucesso.</response>
    /// <response code="404">Lista não encontrada ou não pertence ao usuário.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteList(string id)
    {
        await _deleteList.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    /// <summary>Retorna os produtos da lista ordenados por distância do preço-alvo.</summary>
    /// <remarks>
    /// O campo <c>distancePercent</c> indica quanto o preço atual está acima do alvo em %.
    /// Valor negativo significa que o produto já está abaixo do preço-alvo.
    /// Produtos mais próximos (menor distância) aparecem primeiro.
    /// </remarks>
    /// <param name="id">ID da lista.</param>
    /// <response code="200">Lista de análise ordenada.</response>
    /// <response code="404">Lista não encontrada.</response>
    [HttpGet("{id}/analysis")]
    [ProducesResponseType(typeof(IEnumerable<AnalysisItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetListAnalysis(string id)
    {
        var result = await _getListAnalysis.ExecuteAsync(id, GetUserId());
        return Ok(result);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
