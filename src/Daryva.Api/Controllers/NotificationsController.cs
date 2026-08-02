using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ITenantContext tenantContext,
        AppDbContext dbContext,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Messaging.View)]
    public async Task<ActionResult<IEnumerable<NotificationResponse>>> GetNotifications(
        [FromQuery] NotificationFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var notifications = await _notificationService.GetNotificationsAsync(filter, cancellationToken);
        var response = notifications.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{notificationId:guid}")]
    [Authorize(Policy = Permissions.Messaging.View)]
    public async Task<ActionResult<NotificationResponse>> GetNotification(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var notification = await _notificationService.GetNotificationByIdAsync(notificationId, cancellationToken);
        if (notification == null)
            return NotFound();

        return Ok(MapToResponse(notification));
    }

    [HttpGet("recipients")]
    [Authorize(Policy = Permissions.Messaging.View)]
    public async Task<ActionResult<IEnumerable<NotificationRecipientResponse>>> GetRecipients(
        [FromQuery] RecipientFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var recipients = await _notificationService.BuildRecipientsAsync(filter, cancellationToken);
        return Ok(recipients);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult<NotificationResponse>> CreateNotification(
        [FromBody] CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant == null)
            return BadRequest(new { error = "Tenant not found." });

        var toAddress = string.Equals(request.Channel, "Email", StringComparison.OrdinalIgnoreCase)
            ? tenant.Email
            : tenant.PhoneNumber ?? string.Empty;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenantContext.CurrentOrgId.Value,
            TenantId = request.TenantId,
            TenancyId = request.TenancyId,
            Channel = request.Channel,
            Type = request.Type,
            ToAddress = toAddress,
            Subject = request.Subject,
            Body = request.Body,
            ScheduledFor = NormalizeToUtc(request.ScheduledFor),
            Status = "Pending",
            TemplateId = request.TemplateId
        };

        var created = await _notificationService.CreateNotificationAsync(notification, cancellationToken);
        var loaded = await _notificationService.GetNotificationByIdAsync(created.Id, cancellationToken);
        return CreatedAtAction(nameof(GetNotification), new { notificationId = created.Id }, MapToResponse(loaded ?? created));
    }

    [HttpPost("{notificationId:guid}/send")]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult> SendNotification(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification == null)
            return NotFound();

        var success = await _notificationService.SendNotificationAsync(notification, cancellationToken);
        return Ok(new { success });
    }

    [HttpPost("{notificationId:guid}/send-with-content")]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult> SendNotificationWithContent(
        Guid notificationId,
        [FromBody] SendNotificationWithContentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification == null)
            return NotFound();

        var success = await _notificationService.SendNotificationWithContentAsync(notification, request.ToAddress, request.Subject, request.Body, cancellationToken);
        return Ok(new { success });
    }

    [HttpPost("send-batch")]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult> SendBatch(
        [FromBody] SendBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var success = await _notificationService.SendBatchAsync(request.NotificationIds, cancellationToken);
        return Ok(new { success });
    }

    [HttpPost("process-due")]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult> ProcessDueQueue(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var sent = await _notificationService.ProcessDueQueueAsync(cancellationToken);
        return Ok(new { sent });
    }

    [HttpPost("{notificationId:guid}/cancel")]
    [Authorize(Policy = Permissions.Messaging.Send)]
    public async Task<ActionResult> CancelNotification(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification == null)
            return NotFound();

        await _notificationService.CancelNotificationAsync(notification, cancellationToken);
        return Ok(new { success = true });
    }

    private static NotificationResponse MapToResponse(Notification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            TenantId = notification.TenantId,
            TenancyId = notification.TenancyId,
            Channel = notification.Channel,
            Type = notification.Type,
            ToAddress = notification.ToAddress,
            Subject = notification.Subject,
            Body = notification.Body,
            ScheduledFor = notification.ScheduledFor,
            SentAt = notification.SentAt,
            Status = notification.Status,
            ProviderMessageId = notification.ProviderMessageId,
            Error = notification.Error,
            TemplateId = notification.TemplateId,
            AttemptCount = notification.Attempts?.Count ?? 0,
            TenantName = notification.Tenant?.FullName ?? string.Empty,
            HouseAddressLine1 = notification.Tenancy?.House?.AddressLine1 ?? "Unknown",
            HouseCity = notification.Tenancy?.House?.City ?? string.Empty
        };
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}

public class SendBatchRequest
{
    public List<Guid> NotificationIds { get; set; } = new();
}
