using Daryva.Api.Services.Seed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Development-only seeding endpoints.
/// These endpoints only work when DevAuth is enabled.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly ILogger<SeedController> _logger;
    private readonly IConfiguration _configuration;

    public SeedController(IDataSeeder dataSeeder, ILogger<SeedController> logger, IConfiguration configuration)
    {
        _dataSeeder = dataSeeder;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Seed sample data (development only).
    /// 
    /// Creates a sample organization and houses for the dev user if they don't already exist.
    /// Only works when DevAuth is enabled in appsettings.
    /// 
    /// Example:
    /// POST /api/seed
    /// </summary>
    /// <returns>Status message</returns>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<SeedResponse>> Seed(CancellationToken cancellationToken = default)
    {
        try
        {
            var devAuthEnabled = _configuration.GetValue<bool>("DevAuth:Enabled");

            if (!devAuthEnabled)
            {
                _logger.LogWarning("Seed endpoint called when DevAuth is disabled");
                return BadRequest(new { error = "Seed endpoint is only available when DevAuth is enabled" });
            }

            await _dataSeeder.SeedIfNeededAsync(cancellationToken);

            return Ok(new SeedResponse
            {
                Message = "Sample data seeded successfully",
                DevAuthEnabled = true,
                DevUserId = _configuration.GetValue<string>("DevAuth:UserId") ?? "dev-user-1",
                DevUserEmail = _configuration.GetValue<string>("DevAuth:Email") ?? "dev@local",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding data");
            return StatusCode(500, new { error = "Error seeding data", message = ex.Message });
        }
    }
}

/// <summary>
/// Response from seed endpoint.
/// </summary>
public class SeedResponse
{
    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether DevAuth is enabled.
    /// </summary>
    public bool DevAuthEnabled { get; set; }

    /// <summary>
    /// Dev user ID.
    /// </summary>
    public string DevUserId { get; set; } = string.Empty;

    /// <summary>
    /// Dev user email.
    /// </summary>
    public string DevUserEmail { get; set; } = string.Empty;
}
