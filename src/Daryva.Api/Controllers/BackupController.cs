using Daryva.Api.Dtos;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly ITenantContext _tenantContext;

    public BackupController(IBackupService backupService, ITenantContext tenantContext)
    {
        _backupService = backupService;
        _tenantContext = tenantContext;
    }

    [HttpGet("export")]
    public async Task<ActionResult<BulkImportRequest>> Export(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var export = await _backupService.ExportAsync(cancellationToken);
        return Ok(export);
    }
}
