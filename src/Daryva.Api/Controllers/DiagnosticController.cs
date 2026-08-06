using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Diagnostic endpoints for debugging data issues.
/// DEVELOPMENT ONLY - should be removed in production.
/// Bypasses organization filtering to show raw data counts.
///
/// Gated on BOTH a real platform-admin JWT (Authorize below) AND DevAuth:Enabled (checked in
/// IDiagnosticService) -- previously only the DevAuth:Enabled check existed, with no [Authorize] at
/// all, so these actions were reachable by a fully anonymous, unauthenticated caller. Currently safe
/// only because DevAuth:Enabled is false in production; this is defense in depth against that single
/// flag ever being misconfigured.
/// </summary>
[ApiController]
[Route("api/diagnostic")]
[Authorize(Policy = Permissions.Platform.Admin)]
public class DiagnosticController : ControllerBase
{
    private readonly IDiagnosticService _diagnosticService;
    private readonly ILogger<DiagnosticController> _logger;

    public DiagnosticController(IDiagnosticService diagnosticService, ILogger<DiagnosticController> logger)
    {
        _diagnosticService = diagnosticService;
        _logger = logger;
    }

    /// <summary>
    /// Get raw data counts without organization filtering.
    /// Development only.
    /// </summary>
    [HttpGet("data-counts")]
    public async Task<ActionResult> GetDataCounts()
    {
        try
        {
            var result = await _diagnosticService.GetDataCountsAsync();
            if (result == null)
                return BadRequest(new { error = "Diagnostic endpoints disabled. DevAuth not enabled." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diagnostic data counts");
            return StatusCode(500, new { error = "Error retrieving data", message = ex.Message });
        }
    }

    /// <summary>
    /// Get organization memberships.
    /// Development only.
    /// </summary>
    [HttpGet("org-members")]
    public async Task<ActionResult> GetOrgMembers()
    {
        try
        {
            var result = await _diagnosticService.GetOrgMembersAsync();
            if (result == null)
                return BadRequest(new { error = "Diagnostic endpoints disabled. DevAuth not enabled." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting org members");
            return StatusCode(500, new { error = "Error retrieving data", message = ex.Message });
        }
    }
}
