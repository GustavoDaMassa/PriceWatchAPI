using Microsoft.AspNetCore.Mvc;
using PriceWatch.Application.DTOs.Auth;
using PriceWatch.Application.UseCases.Auth;

namespace PriceWatch.API.Controllers;

[ApiController]
[Route("api/auth")]
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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        await _register.ExecuteAsync(request);
        return StatusCode(201, new { message = "Registration successful. Please verify your email." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _login.ExecuteAsync(request);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        await _verifyEmail.ExecuteAsync(request.Email, request.Token);
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        await _resendVerification.ExecuteAsync(request.Email);
        return Ok(new { message = "Verification email sent." });
    }
}

public record VerifyEmailRequest(string Email, string Token);
public record ResendVerificationRequest(string Email);
