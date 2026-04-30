using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.ProductList;
using PriceWatch.Application.UseCases.ProductList;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

[ApiController]
[Route("api/lists")]
[Authorize]
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

    [HttpGet]
    public async Task<IActionResult> GetUserLists()
    {
        var result = await _getUserLists.ExecuteAsync(GetUserId());
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateList([FromBody] CreateProductListRequest request)
    {
        var result = await _createList.ExecuteAsync(GetUserId(), request);
        return StatusCode(201, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateList(string id, [FromBody] UpdateProductListRequest request)
    {
        await _updateList.ExecuteAsync(id, GetUserId(), request);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteList(string id)
    {
        await _deleteList.ExecuteAsync(id, GetUserId());
        return NoContent();
    }

    [HttpGet("{id}/analysis")]
    public async Task<IActionResult> GetListAnalysis(string id)
    {
        var result = await _getListAnalysis.ExecuteAsync(id, GetUserId());
        return Ok(result);
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
