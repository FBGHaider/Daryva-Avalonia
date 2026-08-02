using Daryva.Api.Dtos;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantContext _tenantContext;

    public AuthController(IAuthService authService, ITenantContext tenantContext)
    {
        _authService = authService;
        _tenantContext = tenantContext;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _authService.RegisterAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.VerifyEmailAsync(request.Token, cancellationToken);
        if (!result.Verified)
            return BadRequest(result);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmailByQuery([FromQuery] string token, CancellationToken cancellationToken = default)
    {
        var result = await _authService.VerifyEmailAsync(token, cancellationToken);
        if (!result.Verified)
            return BadRequest(result);

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RegisterResponse>> ResendVerification([FromBody] ResendVerificationEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        var result = await _authService.ResendVerificationEmailAsync(request.Email, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (result == null)
                return Unauthorized(new { error = "Invalid credentials." });

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }

    }

    [AllowAnonymous]
    [HttpPost("2fa/verify")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> VerifyTwoFactorLogin([FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.VerifyTwoFactorLoginAsync(request.ChallengeToken, request.Code, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result == null)
            return Unauthorized(new { error = "Invalid or expired code." });

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result == null)
            return Unauthorized(new { error = "Invalid refresh token." });

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken cancellationToken = default)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        var result = await _authService.ForgotPasswordAsync(request.Email, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken = default)
    {
        var me = await _authService.GetMeAsync(_tenantContext.UserId, cancellationToken);
        if (me == null)
            return Unauthorized(new { error = "User not found." });

        return Ok(me);
    }

    [Authorize]
    [HttpPost("2fa/enroll")]
    [ProducesResponseType(typeof(TwoFactorEnrollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TwoFactorEnrollResponse>> EnrollTwoFactor(CancellationToken cancellationToken = default)
    {
        var result = await _authService.EnrollTwoFactorAsync(_tenantContext.UserId, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("2fa/confirm")]
    [ProducesResponseType(typeof(TwoFactorConfirmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TwoFactorConfirmResponse>> ConfirmTwoFactor([FromBody] TwoFactorConfirmRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.ConfirmTwoFactorAsync(_tenantContext.UserId, request.Code, cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    [ProducesResponseType(typeof(TwoFactorDisableResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TwoFactorDisableResponse>> DisableTwoFactor([FromBody] TwoFactorDisableRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.DisableTwoFactorAsync(_tenantContext.UserId, request.Password, cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("2fa/recovery-codes/regenerate")]
    [ProducesResponseType(typeof(TwoFactorRegenerateRecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TwoFactorRegenerateRecoveryCodesResponse>> RegenerateRecoveryCodes([FromBody] TwoFactorRegenerateRecoveryCodesRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _authService.RegenerateRecoveryCodesAsync(_tenantContext.UserId, request.Password, cancellationToken);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
