using Daryva.Api.Domain;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        ITenantContext tenantContext,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetDocuments(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var documents = await _documentService.GetAllDocumentsAsync(cancellationToken);
            var response = documents.Select(MapToResponse).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load documents.", detail = ex.Message });
        }
    }

    [HttpGet("tenant/{tenantId:guid}")]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetDocumentsByTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var documents = await _documentService.GetDocumentsByTenantAsync(tenantId, cancellationToken);
            var response = documents.Select(MapToResponse).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load documents by tenant.", detail = ex.Message });
        }
    }

    [HttpGet("tenancy/{tenancyId:guid}")]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetDocumentsByTenancy(
        Guid tenancyId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var documents = await _documentService.GetDocumentsByTenancyAsync(tenancyId, cancellationToken);
            var response = documents.Select(MapToResponse).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load documents by tenancy.", detail = ex.Message });
        }
    }

    [HttpGet("house/{houseId:guid}")]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetDocumentsByHouse(
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var documents = await _documentService.GetDocumentsByHouseAsync(houseId, cancellationToken);
        var response = documents.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{documentId:guid}")]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<ActionResult<DocumentResponse>> GetDocument(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);
            if (document == null)
                return NotFound();

            return Ok(MapToResponse(document));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load document.", detail = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Documents.Manage)]
    public async Task<ActionResult<DocumentResponse>> CreateDocument(
        [FromBody] CreateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var rawUploaded = request.UploadedAt == default ? DateTime.UtcNow : request.UploadedAt;
            var uploadedAt = rawUploaded.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(rawUploaded, DateTimeKind.Utc)
                : rawUploaded.ToUniversalTime();
            var validFrom = request.ValidFrom.HasValue
                ? (DateTime?) (request.ValidFrom.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ValidFrom.Value, DateTimeKind.Utc) : request.ValidFrom.Value.ToUniversalTime())
                : null;
            var validTo = request.ValidTo.HasValue
                ? (DateTime?) (request.ValidTo.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ValidTo.Value, DateTimeKind.Utc) : request.ValidTo.Value.ToUniversalTime())
                : null;

            var document = new Document
            {
                Id = Guid.NewGuid(),
                OrganizationId = _tenantContext.CurrentOrgId.Value,
                TenantId = request.TenantId,
                TenancyId = request.TenancyId,
                HouseId = request.HouseId,
                Type = request.Type ?? string.Empty,
                DisplayName = request.DisplayName ?? string.Empty,
                FileName = request.FileName ?? string.Empty,
                FileMimeType = request.FileMimeType,
                Source = request.Source ?? "Uploaded",
                UploadedAt = uploadedAt,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Version = 1,
                IsActive = true
            };

            var created = await _documentService.CreateDocumentAsync(document, cancellationToken);
            return CreatedAtAction(nameof(GetDocument), new { documentId = created.Id }, MapToResponse(created));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to create document.", detail = ex.Message });
        }
    }

    [HttpPut("{documentId:guid}")]
    [Authorize(Policy = Permissions.Documents.Manage)]
    public async Task<ActionResult<DocumentResponse>> UpdateDocument(
        Guid documentId,
        [FromBody] UpdateDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);
            if (document == null)
                return NotFound();

            document.DisplayName = request.DisplayName ?? document.DisplayName;
            document.ValidFrom = request.ValidFrom.HasValue
                ? (DateTime?) (request.ValidFrom.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ValidFrom.Value, DateTimeKind.Utc) : request.ValidFrom.Value.ToUniversalTime())
                : document.ValidFrom;
            document.ValidTo = request.ValidTo.HasValue
                ? (DateTime?) (request.ValidTo.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(request.ValidTo.Value, DateTimeKind.Utc) : request.ValidTo.Value.ToUniversalTime())
                : document.ValidTo;
            document.IsActive = request.IsActive;

            await _documentService.UpdateDocumentAsync(document, cancellationToken);
            return Ok(MapToResponse(document));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to update document.", detail = ex.Message });
        }
    }

    [HttpGet("{documentId:guid}/download")]
    [Authorize(Policy = Permissions.Documents.View)]
    public async Task<IActionResult> DownloadDocument(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);
        if (document == null)
            return NotFound();

        // TODO: Implement file download (read from storage path or blob storage)
        // For now, return empty array
        var fileBytes = new byte[0];
        
        return File(fileBytes, document.FileMimeType ?? "application/octet-stream", document.FileName);
    }

    [HttpDelete("{documentId:guid}")]
    [Authorize(Policy = Permissions.Documents.Manage)]
    public async Task<IActionResult> DeleteDocument(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId, cancellationToken);
            if (document == null)
                return NotFound();

            await _documentService.DeleteDocumentAsync(documentId, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to delete document.", detail = ex.Message });
        }
    }

    private static DocumentResponse MapToResponse(Document document)
    {
        return new DocumentResponse
        {
            Id = document.Id,
            TenantId = document.TenantId,
            TenancyId = document.TenancyId,
            HouseId = document.HouseId,
            Type = document.Type,
            DisplayName = document.DisplayName,
            FileName = document.FileName,
            FileMimeType = document.FileMimeType,
            StoragePath = document.StoragePath,
            Source = document.Source,
            UploadedAt = document.UploadedAt,
            ValidFrom = document.ValidFrom,
            ValidTo = document.ValidTo,
            Version = document.Version,
            IsActive = document.IsActive
        };
    }
}

public class CreateDocumentRequest
{
    public Guid? TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid? HouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    /// <summary>Base64-encoded file content.</summary>
    public string? FileContent { get; set; }
}

public class UpdateDocumentRequest
{
    public string? DisplayName { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DocumentResponse
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid? HouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? StoragePath { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
}
