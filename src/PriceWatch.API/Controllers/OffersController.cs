using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Offer;
using PriceWatch.Application.UseCases.Offers;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Ofertas por loja de um produto e comparação de preço entre lojas (pivô multi-loja).</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public class OffersController : ControllerBase
{
    private readonly CreateOfferUseCase _createOffer;
    private readonly GetProductComparisonUseCase _getComparison;
    private readonly RedirectOfferUseCase _redirectOffer;
    private readonly ILogger<OffersController> _logger;

    public OffersController(
        CreateOfferUseCase createOffer,
        GetProductComparisonUseCase getComparison,
        RedirectOfferUseCase redirectOffer,
        ILogger<OffersController> logger)
    {
        _createOffer = createOffer;
        _getComparison = getComparison;
        _redirectOffer = redirectOffer;
        _logger = logger;
    }

    /// <summary>Adiciona uma oferta (URL de uma loja) para acompanhamento.</summary>
    /// <remarks>
    /// O sistema detecta a loja automaticamente pela URL. Se já existir uma oferta pra essa
    /// URL, reaproveita o produto-mestre já existente. O casamento automático com outras
    /// lojas roda de forma assíncrona (não bloqueia esta resposta).
    /// </remarks>
    /// <response code="201">Oferta adicionada e acompanhamento iniciado.</response>
    /// <response code="400">URL não suportada ou produto não encontrado.</response>
    [HttpPost("/api/offers")]
    [ProducesResponseType(typeof(OfferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest request)
    {
        var result = await _createOffer.ExecuteAsync(GetUserId(), request);
        return StatusCode(201, result);
    }

    /// <summary>Retorna as ofertas de um produto-mestre, ordenadas por preço, com a mais barata destacada.</summary>
    /// <param name="id">ID do produto-mestre.</param>
    /// <response code="200">Comparação entre as lojas que têm o produto.</response>
    /// <response code="404">Produto não encontrado.</response>
    [HttpGet("/api/products/{id}/comparison")]
    [ProducesResponseType(typeof(ProductComparisonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComparison(string id)
    {
        var result = await _getComparison.ExecuteAsync(id);
        return Ok(result);
    }

    /// <summary>Redireciona para a loja através do link de afiliado, registrando o clique.</summary>
    /// <param name="id">ID da oferta.</param>
    /// <response code="302">Redireciona para a URL de afiliado (ou original, se nenhuma rede estiver configurada pra essa loja ainda).</response>
    /// <response code="404">Oferta não encontrada.</response>
    [HttpGet("/api/offers/{id}/redirect")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedirectToOffer(string id)
    {
        var result = await _redirectOffer.ExecuteAsync(id, GetUserId());

        _logger.LogInformation(
            "Affiliate redirect click: offer {OfferId}, source {Source}, user {UserId}",
            result.OfferId, result.Source, GetUserId());

        return Redirect(result.AffiliateUrl);
    }

    /// <summary>Registra o clique e devolve a URL da loja (link de afiliado) em JSON.</summary>
    /// <remarks>
    /// Equivalente ao GET /redirect para clientes que não conseguem anexar o JWT a uma navegação
    /// (SPA, extensão, app): o cliente chama este endpoint e abre a URL devolvida.
    /// </remarks>
    /// <param name="id">ID da oferta.</param>
    /// <response code="200">URL de destino.</response>
    /// <response code="404">Oferta não encontrada.</response>
    [HttpPost("/api/offers/{id}/click")]
    [ProducesResponseType(typeof(OfferClickResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClickOffer(string id)
    {
        var result = await _redirectOffer.ExecuteAsync(id, GetUserId());

        _logger.LogInformation(
            "Affiliate click: offer {OfferId}, source {Source}, user {UserId}",
            result.OfferId, result.Source, GetUserId());

        return Ok(new OfferClickResponse(result.AffiliateUrl));
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}

public record OfferClickResponse(string Url);
