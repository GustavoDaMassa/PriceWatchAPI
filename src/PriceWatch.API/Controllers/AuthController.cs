using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Auth;
using PriceWatch.Application.UseCases.Auth;

namespace PriceWatch.API.Controllers;

/// <summary>Autenticação e gerenciamento de conta.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RegisterUseCase _register;
    private readonly LoginUseCase _login;
    private readonly VerifyEmailUseCase _verifyEmail;
    private readonly ResendVerificationUseCase _resendVerification;

    public AuthController(
        RegisterUseCase register,
        LoginUseCase login,
        VerifyEmailUseCase verifyEmail,
        ResendVerificationUseCase resendVerification)
    {
        _register = register;
        _login = login;
        _verifyEmail = verifyEmail;
        _resendVerification = resendVerification;
    }

    /// <summary>Cria uma nova conta de usuário.</summary>
    /// <remarks>Um email de verificação é enviado automaticamente após o registro. O login só é permitido após verificar o email.</remarks>
    /// <response code="201">Conta criada. Verifique o email para ativar.</response>
    /// <response code="400">Email já está em uso.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        await _register.ExecuteAsync(request);
        return StatusCode(201, new { message = "Registration successful. Please verify your email." });
    }

    /// <summary>Autentica o usuário e retorna um token JWT.</summary>
    /// <remarks>O email deve ter sido verificado antes do login. O token expira em 24 horas.</remarks>
    /// <response code="200">Login realizado. Retorna token JWT, email e nome.</response>
    /// <response code="400">Credenciais inválidas ou email não verificado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _login.ExecuteAsync(request);
        return Ok(result);
    }

    /// <summary>Confirma o email do usuário usando o token recebido por email.</summary>
    /// <remarks>O token é enviado no email de registro ou de reenvio. Válido por 24 horas.</remarks>
    /// <response code="200">Email verificado com sucesso.</response>
    /// <response code="400">Token inválido ou expirado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        await _verifyEmail.ExecuteAsync(request.Email, request.Token);
        return Ok(new { message = "Email verified successfully." });
    }

    /// <summary>Reenvia o email de verificação.</summary>
    /// <remarks>Use quando o email anterior expirou ou não foi recebido. Gera um novo token.</remarks>
    /// <response code="200">Email reenviado.</response>
    /// <response code="400">Email já verificado.</response>
    /// <response code="404">Usuário não encontrado.</response>
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        await _resendVerification.ExecuteAsync(request.Email);
        return Ok(new { message = "Verification email sent." });
    }
}

public record VerifyEmailRequest(string Email, string Token);
public record ResendVerificationRequest(string Email);
