using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/notification-templates")]
[Authorize]
public class NotificationTemplatesController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;

    public NotificationTemplatesController(INotificationService notificationService, ITenantContext tenantContext)
    {
        _notificationService = notificationService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationTemplateResponse>>> GetTemplates(
        [FromQuery] string? channel,
        [FromQuery] string? type,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var templates = await _notificationService.GetTemplatesAsync(channel, type, cancellationToken);
        var response = templates.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{templateId:guid}")]
    public async Task<ActionResult<NotificationTemplateResponse>> GetTemplate(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var template = await _notificationService.GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null)
            return NotFound();

        return Ok(MapToResponse(template));
    }

    [HttpPost]
    public async Task<ActionResult<NotificationTemplateResponse>> CreateTemplate(
        [FromBody] NotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var template = new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenantContext.CurrentOrgId.Value,
            Name = request.Name,
            Channel = request.Channel,
            Type = request.Type,
            SubjectTemplate = request.SubjectTemplate,
            BodyTemplate = request.BodyTemplate,
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _notificationService.CreateTemplateAsync(template, cancellationToken);
        return CreatedAtAction(nameof(GetTemplate), new { templateId = created.Id }, MapToResponse(created));
    }

    [HttpPut("{templateId:guid}")]
    public async Task<ActionResult<NotificationTemplateResponse>> UpdateTemplate(
        Guid templateId,
        [FromBody] NotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var template = await _notificationService.GetTemplateByIdAsync(templateId, cancellationToken);
        if (template == null)
            return NotFound();

        template.Name = request.Name;
        template.Channel = request.Channel;
        template.Type = request.Type;
        template.SubjectTemplate = request.SubjectTemplate;
        template.BodyTemplate = request.BodyTemplate;
        template.IsDefault = request.IsDefault;

        await _notificationService.UpdateTemplateAsync(template, cancellationToken);
        return Ok(MapToResponse(template));
    }

    private static NotificationTemplateResponse MapToResponse(NotificationTemplate template)
    {
        return new NotificationTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            Channel = template.Channel,
            Type = template.Type,
            SubjectTemplate = template.SubjectTemplate,
            BodyTemplate = template.BodyTemplate,
            IsDefault = template.IsDefault,
            CreatedAt = template.CreatedAt
        };
    }
}
