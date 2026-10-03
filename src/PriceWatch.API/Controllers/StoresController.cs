using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Store;
using PriceWatch.Application.UseCases.Stores;

namespace PriceWatch.API.Controllers;

/// <summary>Lojas suportadas e o que a plataforma consegue fazer com cada uma.</summary>
[ApiController]
[Route("api/stores")]
[Authorize]
[Produces("application/json")]
public class StoresController : ControllerBase
{
    private readonly GetSupportedStoresUseCase _getStores;

    public StoresController(GetSupportedStoresUseCase getStores)
    {
        _getStores = getStores;
    }

    /// <summary>Lista as lojas com nome, página inicial e capacidades (buscar por URL, buscar ofertas, gerar link de afiliado).</summary>
    /// <response code="200">Lojas conhecidas e suas capacidades.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StoreCapabilitiesResponse>), StatusCodes.Status200OK)]
    public IActionResult GetStores() => Ok(_getStores.Execute());
}
