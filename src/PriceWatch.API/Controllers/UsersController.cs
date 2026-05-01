using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Users;
using PriceWatch.Application.UseCases.Users;
using PriceWatch.Domain.Exceptions;

namespace PriceWatch.API.Controllers;

/// <summary>Gerenciamento do perfil do usuário autenticado.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly GetProfileUseCase _getProfile;
    private readonly ChangePasswordUseCase _changePassword;
    private readonly ChangeEmailUseCase _changeEmail;
    private readonly DeleteAccountUseCase _deleteAccount;

    public UsersController(
        GetProfileUseCase getProfile,
        ChangePasswordUseCase changePassword,
        ChangeEmailUseCase changeEmail,
        DeleteAccountUseCase deleteAccount)
    {
        _getProfile = getProfile;
        _changePassword = changePassword;
        _changeEmail = changeEmail;
        _deleteAccount = deleteAccount;
    }

    /// <summary>Retorna o perfil do usuário autenticado.</summary>
    /// <response code="200">Perfil do usuário: id, nome, email, status de verificação e data de criação.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile()
    {
        var result = await _getProfile.ExecuteAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>Altera a senha do usuário.</summary>
    /// <remarks>Requer a senha atual para confirmação. A nova senha entra em vigor imediatamente.</remarks>
    /// <response code="204">Senha alterada com sucesso.</response>
    /// <response code="400">Senha atual incorreta.</response>
    [HttpPatch("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        await _changePassword.ExecuteAsync(GetUserId(), request);
        return NoContent();
    }

    /// <summary>Altera o email do usuário.</summary>
    /// <remarks>
    /// O email é trocado imediatamente e a conta volta a exigir verificação.
    /// Um email de verificação é enviado automaticamente para o novo endereço.
    /// O login só será possível novamente após verificar o novo email.
    /// </remarks>
    /// <response code="204">Email alterado. Verifique o novo endereço para reativar a conta.</response>
    /// <response code="400">Novo email já está em uso.</response>
    [HttpPatch("me/email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        await _changeEmail.ExecuteAsync(GetUserId(), request);
        return NoContent();
    }

    /// <summary>Exclui permanentemente a conta e todos os dados associados.</summary>
    /// <remarks>
    /// Remove em cascata: listas, produtos monitorados, histórico de preços e notificações.
    /// Requer confirmação da senha. A operação é irreversível.
    /// </remarks>
    /// <response code="204">Conta excluída com sucesso.</response>
    /// <response code="400">Senha incorreta.</response>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        await _deleteAccount.ExecuteAsync(GetUserId(), request.Password);
        return NoContent();
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new BusinessException("User ID not found in token.");
}

public record DeleteAccountRequest(string Password);
