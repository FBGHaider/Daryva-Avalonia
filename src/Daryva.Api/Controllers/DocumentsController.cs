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
    // Decoded (pre-base64) size. Base64 inflates bytes by ~4/3, so this keeps the encoded
    // JSON body comfortably under nginx's client_max_body_size 25m (deploy/nginx/daryva-api.conf)
    // and Kestrel's own MaxRequestBodySize (Program.cs) - both operate on the raw encoded body.
    private const long MaxFileSizeBytes = 18 * 1024 * 1024;

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
            // A Tenant caller only ever sees their own documents -- the org-wide query filter
            // alone isn't enough to isolate one tenant from another within the same org.
            var documents = _tenantContext.CurrentRole == Roles.Tenant
                ? await _documentService.GetDocumentsByTenantAsync(_tenantContext.CurrentTenantId ?? Guid.Empty, cancellationToken)
                : await _documentService.GetAllDocumentsAsync(cancellationToken);
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

        if (_tenantContext.CurrentRole == Roles.Tenant && tenantId != _tenantContext.CurrentTenantId)
            return Forbid();

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
            var response = documents.Where(IsVisibleToCaller).Select(MapToResponse).ToList();
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
        var response = documents.Where(IsVisibleToCaller).Select(MapToResponse).ToList();
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
            if (document == null || !IsVisibleToCaller(document))
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

        byte[]? fileContent = null;
        if (!string.IsNullOrWhiteSpace(request.FileContent))
        {
            try
            {
                fileContent = Convert.FromBase64String(request.FileContent);
            }
            catch (FormatException)
            {
                return BadRequest(new { error = "FileContent is not valid base64." });
            }

            if (fileContent.Length > MaxFileSizeBytes)
                return BadRequest(new { error = $"File exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB." });
        }

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

            var created = await _documentService.CreateDocumentAsync(document, fileContent, cancellationToken);
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
        if (document == null || !IsVisibleToCaller(document))
            return NotFound();

        var fileBytes = await _documentService.GetFileContentAsync(document, cancellationToken);
        if (fileBytes == null)
            return NotFound(new { error = "File not available for this document." });

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

    /// <summary>
    /// A Tenant caller may only see documents explicitly linked to their own TenantId -- house-only
    /// documents (TenantId null, e.g. a whole-property gas safety certificate) are deliberately
    /// excluded from the portal for this first cut rather than guessed at. Every other role passes
    /// through unchanged; the org-wide query filter already isolates them from other orgs.
    /// </summary>
    private bool IsVisibleToCaller(Document document)
        => _tenantContext.CurrentRole != Roles.Tenant || document.TenantId == _tenantContext.CurrentTenantId;

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
