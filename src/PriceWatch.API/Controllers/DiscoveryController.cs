using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Discovery;
using PriceWatch.Application.UseCases.Discovery;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Descoberta do mesmo produto em outras lojas e confirmação pelo usuário.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public class DiscoveryController : ControllerBase
{
    private readonly DiscoverOffersUseCase _discover;
    private readonly GetOfferCandidatesUseCase _getCandidates;
    private readonly ConfirmOfferMatchUseCase _confirm;

    public DiscoveryController(
        DiscoverOffersUseCase discover,
        GetOfferCandidatesUseCase getCandidates,
        ConfirmOfferMatchUseCase confirm)
    {
        _discover = discover;
        _getCandidates = getCandidates;
        _confirm = confirm;
    }

    /// <summary>Procura o produto nas outras lojas e devolve as ofertas candidatas.</summary>
    /// <remarks>
    /// Usa o GTIN quando existe e, sem resultado, busca por palavra-chave; cada resultado é pontuado.
    /// A busca roda no máximo uma vez por produto a cada 6 horas (compartilhada entre usuários); nas
    /// chamadas seguintes devolve as candidatas já encontradas. Candidatas NÃO são monitoradas até a confirmação.
    /// </remarks>
    /// <param name="productId">ID do produto-mestre (<c>productId</c> das respostas de oferta e de itens).</param>
    /// <response code="200">Candidatas que o usuário ainda não julgou.</response>
    /// <response code="404">O usuário não acompanha este produto.</response>
    [HttpPost("/api/products/{productId}/discover")]
    [ProducesResponseType(typeof(IReadOnlyList<OfferCandidateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Discover(string productId)
    {
        await _discover.ExecuteAsync(GetUserId(), productId);
        return Ok(await _getCandidates.ExecuteAsync(GetUserId(), productId));
    }

    /// <summary>Candidatas válidas do produto que o usuário ainda não julgou.</summary>
    /// <response code="200">Lista de candidatas, da mais para a menos provável.</response>
    /// <response code="404">O usuário não acompanha este produto.</response>
    [HttpGet("/api/products/{productId}/candidates")]
    [ProducesResponseType(typeof(IReadOnlyList<OfferCandidateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCandidates(string productId) =>
        Ok(await _getCandidates.ExecuteAsync(GetUserId(), productId));

    /// <summary>"É o mesmo produto?" e, se quiser, acompanhar a oferta.</summary>
    /// <remarks>
    /// O veredito é sempre gravado. A oferta só é anexada ao produto (comparação e monitoramento) se a
    /// candidata veio por GTIN idêntico, se você é o único assinante do produto ou se outro usuário
    /// também confirmou; senão o resultado é <c>Pending</c>.
    /// </remarks>
    /// <param name="id">ID da candidata.</param>
    /// <response code="200">Resultado: Rejected, Recorded, Attached, Pending ou Unavailable.</response>
    /// <response code="404">Candidata inexistente, expirada ou de produto que o usuário não acompanha.</response>
    [HttpPost("/api/candidates/{id}/feedback")]
    [ProducesResponseType(typeof(ConfirmOfferMatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Feedback(string id, [FromBody] ConfirmOfferMatchRequest request) =>
        Ok(await _confirm.ExecuteAsync(GetUserId(), id, request));

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}
