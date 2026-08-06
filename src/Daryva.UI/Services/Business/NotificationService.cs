using System.Globalization;
using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Adapter that implements INotificationService using the backend API.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationApiService _notificationApiService;
    private readonly ISettingsService _settingsService;
    private readonly ITenantService _tenantService;
    private readonly IHouseService _houseService;

    private readonly Dictionary<int, Guid> _notificationIdMap = new();
    private readonly Dictionary<int, Guid> _templateIdMap = new();
    private readonly Dictionary<int, Guid> _tenantIdMap = new();
    private readonly Dictionary<int, Guid> _houseIdMap = new();

    public NotificationService(
        INotificationApiService notificationApiService,
        ISettingsService settingsService,
        ITenantService tenantService,
        IHouseService houseService)
    {
        _notificationApiService = notificationApiService ?? throw new ArgumentNullException(nameof(notificationApiService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
    }

    public async Task<IEnumerable<NotificationRecipient>> BuildRecipientsAsync(RecipientFilter filter)
    {
        var apiFilter = new RecipientFilterDto
        {
            TargetType = filter.TargetType,
            TenantId = filter.TenantId.HasValue ? await ResolveTenantGuidAsync(filter.TenantId.Value) : null,
            HouseId = filter.HouseId.HasValue ? await ResolveHouseGuidAsync(filter.HouseId.Value) : null,
            StatusFilter = filter.StatusFilter,
            Month = filter.Month,
            Year = filter.Year
        };

        var recipients = await _notificationApiService.GetRecipientsAsync(apiFilter);
        return recipients.Select(r => new NotificationRecipient
        {
            TenantId = r.TenantId.GetHashCode(),
            ApiTenantId = r.TenantId,
            TenantName = r.TenantName,
            Email = r.Email,
            PhoneNumber = r.PhoneNumber,
            TenancyId = r.TenancyId?.GetHashCode(),
            ApiTenancyId = r.TenancyId,
            HouseAddress = string.IsNullOrWhiteSpace(r.HouseAddressLine1) ? "Unknown" : $"{r.HouseAddressLine1}, {r.HouseCity}".TrimEnd(' ', ','),
            HasEmail = r.HasEmail,
            HasWhatsApp = r.HasWhatsApp,
            AmountDue = r.AmountDue,
            DueDate = r.DueDate
        }).ToList();
    }

    public async Task<string> RenderTemplateAsync(string templateBody, string? subjectTemplate, NotificationContext context)
    {
        var gbp = CultureInfo.GetCultureInfo("en-GB");
        var currency = await _settingsService.GetSettingAsync("Currency", "GBP") ?? "GBP";
        var symbol = ResolveCurrencySymbol(currency);

        var amountDueStr = context.AmountDue.HasValue ? context.AmountDue.Value.ToString("N2", gbp) : "0.00";
        var depositStr = context.DepositRemaining.HasValue ? context.DepositRemaining.Value.ToString("N2", gbp) : "0.00";

        var rendered = templateBody
            .Replace("{TenantName}", context.TenantName)
            .Replace("{HouseAddress}", context.HouseAddress)
            .Replace("{Currency}", symbol)
            .Replace("{AmountDue}", amountDueStr)
            .Replace("{DueDate}", context.DueDate?.ToString("dd MMMM yyyy", gbp) ?? "")
            .Replace("{Month}", context.Month)
            .Replace("{DepositRemaining}", depositStr)
            .Replace("{PayInstructions}", context.PayInstructions)
            .Replace("{Message}", context.Message ?? "");

        return rendered;
    }

    public async Task<Notification> QueueNotificationAsync(NotificationDto dto)
    {
        if (!dto.TenantApiId.HasValue)
            throw new InvalidOperationException("Tenant API ID is required for notifications.");

        var request = new CreateNotificationRequestDto
        {
            TenantId = dto.TenantApiId.Value,
            TenancyId = dto.TenancyApiId,
            Channel = dto.Channel,
            Type = dto.Type,
            Subject = dto.Subject,
            Body = dto.Body,
            ScheduledFor = dto.ScheduledFor,
            TemplateId = await ResolveTemplateGuidAsync(dto.TemplateId, dto.TemplateApiId)
        };

        var created = await _notificationApiService.CreateNotificationAsync(request);
        var notification = MapToNotification(created);
        _notificationIdMap[notification.NotificationId] = created.Id;
        return notification;
    }

    public async Task<bool> SendNotificationAsync(int notificationId)
    {
        var apiId = await ResolveNotificationGuidAsync(notificationId);
        return await _notificationApiService.SendNotificationAsync(apiId);
    }

    public async Task<bool> SendNotificationWithContentAsync(int notificationId, string toAddress, string subject, string body)
    {
        var apiId = await ResolveNotificationGuidAsync(notificationId);
        return await _notificationApiService.SendNotificationWithContentAsync(apiId, new SendNotificationWithContentRequestDto
        {
            ToAddress = toAddress,
            Subject = subject,
            Body = body
        });
    }

    public async Task<bool> SendBatchAsync(IEnumerable<int> notificationIds)
    {
        var apiIds = new List<Guid>();
        foreach (var id in notificationIds)
        {
            apiIds.Add(await ResolveNotificationGuidAsync(id));
        }
        return await _notificationApiService.SendBatchAsync(apiIds);
    }

    public async Task<int> ProcessDueQueueAsync()
    {
        return await _notificationApiService.ProcessDueQueueAsync();
    }

    public async Task<IEnumerable<Notification>> GetNotificationsAsync(NotificationFilter filter)
    {
        var apiFilter = new NotificationFilterDto
        {
            Status = filter.Status,
            StartDate = filter.StartDate,
            EndDate = filter.EndDate,
            Channel = filter.Channel,
            Type = filter.Type,
            TenantId = filter.TenantId.HasValue ? await ResolveTenantGuidAsync(filter.TenantId.Value) : null,
            HouseId = filter.HouseId.HasValue ? await ResolveHouseGuidAsync(filter.HouseId.Value) : null
        };

        var notifications = await _notificationApiService.GetNotificationsAsync(apiFilter);
        var mapped = notifications.Select(MapToNotification).ToList();
        foreach (var n in mapped)
        {
            if (n.ApiId.HasValue)
                _notificationIdMap[n.NotificationId] = n.ApiId.Value;
        }
        return mapped;
    }

    public async Task<Notification?> GetNotificationByIdAsync(int notificationId)
    {
        var apiId = await ResolveNotificationGuidAsync(notificationId);
        var notification = await _notificationApiService.GetNotificationByIdAsync(apiId);
        return notification == null ? null : MapToNotification(notification);
    }

    public async Task CancelNotificationAsync(int notificationId)
    {
        var apiId = await ResolveNotificationGuidAsync(notificationId);
        await _notificationApiService.CancelNotificationAsync(apiId);
    }

    public async Task<IEnumerable<NotificationTemplate>> GetTemplatesAsync(string? channel = null, string? type = null)
    {
        var templates = await _notificationApiService.GetTemplatesAsync(channel, type);
        var mapped = templates.Select(MapToTemplate).ToList();
        foreach (var t in mapped)
        {
            if (t.ApiId.HasValue)
                _templateIdMap[t.TemplateId] = t.ApiId.Value;
        }
        return mapped;
    }

    public async Task<NotificationTemplate?> GetTemplateByIdAsync(int templateId)
    {
        var apiId = await ResolveTemplateGuidAsync(templateId, null);
        if (!apiId.HasValue)
            return null;

        var template = await _notificationApiService.GetTemplateByIdAsync(apiId.Value);
        return template == null ? null : MapToTemplate(template);
    }

    private Notification MapToNotification(NotificationResponseDto dto)
    {
        var notification = new Notification
        {
            NotificationId = dto.Id.GetHashCode(),
            ApiId = dto.Id,
            TenantId = dto.TenantId.GetHashCode(),
            TenancyId = dto.TenancyId?.GetHashCode(),
            Channel = dto.Channel,
            Type = dto.Type,
            ToAddress = dto.ToAddress,
            Subject = dto.Subject,
            Body = dto.Body,
            ScheduledFor = dto.ScheduledFor,
            SentAt = dto.SentAt,
            Status = dto.Status,
            ProviderMessageId = dto.ProviderMessageId,
            Error = dto.Error,
            TemplateId = dto.TemplateId?.GetHashCode()
        };

        notification.Tenant = new Tenant { FullName = dto.TenantName };
        notification.Tenancy = new Tenancy
        {
            House = new House
            {
                AddressLine1 = dto.HouseAddressLine1,
                City = dto.HouseCity
            }
        };

        return notification;
    }

    private NotificationTemplate MapToTemplate(NotificationTemplateDto dto)
    {
        return new NotificationTemplate
        {
            TemplateId = dto.Id.GetHashCode(),
            ApiId = dto.Id,
            Name = dto.Name,
            Channel = dto.Channel,
            Type = dto.Type,
            SubjectTemplate = dto.SubjectTemplate,
            BodyTemplate = dto.BodyTemplate,
            IsDefault = dto.IsDefault,
            CreatedAt = dto.CreatedAt
        };
    }

    private async Task<Guid> ResolveNotificationGuidAsync(int notificationId)
    {
        if (_notificationIdMap.TryGetValue(notificationId, out var apiId))
            return apiId;

        var notifications = await _notificationApiService.GetNotificationsAsync(new NotificationFilterDto());
        foreach (var n in notifications)
        {
            _notificationIdMap[n.Id.GetHashCode()] = n.Id;
        }

        if (_notificationIdMap.TryGetValue(notificationId, out apiId))
            return apiId;

        throw new InvalidOperationException("Notification not found in API.");
    }

    private async Task<Guid?> ResolveTemplateGuidAsync(int? templateId, Guid? templateApiId)
    {
        if (templateApiId.HasValue)
            return templateApiId.Value;

        if (!templateId.HasValue)
            return null;

        if (_templateIdMap.TryGetValue(templateId.Value, out var apiId))
            return apiId;

        var templates = await _notificationApiService.GetTemplatesAsync();
        foreach (var t in templates)
        {
            _templateIdMap[t.Id.GetHashCode()] = t.Id;
        }

        if (_templateIdMap.TryGetValue(templateId.Value, out apiId))
            return apiId;

        return null;
    }

    private Task<Guid?> ResolveTenantGuidAsync(int tenantId)
    {
        if (_tenantIdMap.TryGetValue(tenantId, out var apiId))
            return Task.FromResult<Guid?>(apiId);

        return ResolveTenantGuidFromServiceAsync(tenantId);
    }

    private Task<Guid?> ResolveHouseGuidAsync(int houseId)
    {
        if (_houseIdMap.TryGetValue(houseId, out var apiId))
            return Task.FromResult<Guid?>(apiId);

        return ResolveHouseGuidFromServiceAsync(houseId);
    }

    private async Task<Guid?> ResolveTenantGuidFromServiceAsync(int tenantId)
    {
        var tenants = await _tenantService.GetAllTenantsAsync();
        foreach (var tenant in tenants)
        {
            if (tenant.ApiId.HasValue)
                _tenantIdMap[tenant.TenantId] = tenant.ApiId.Value;
        }

        return _tenantIdMap.TryGetValue(tenantId, out var apiId) ? apiId : null;
    }

    private async Task<Guid?> ResolveHouseGuidFromServiceAsync(int houseId)
    {
        var houses = await _houseService.GetAllHousesAsync();
        foreach (var house in houses)
        {
            if (house.ApiId.HasValue)
                _houseIdMap[house.HouseId] = house.ApiId.Value;
        }

        return _houseIdMap.TryGetValue(houseId, out var apiId) ? apiId : null;
    }

    private static string ResolveCurrencySymbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "£";
        var v = value.Trim();
        return v.ToUpperInvariant() switch
        {
            "GBP" => "£",
            "USD" => "$",
            "EUR" => "€",
            "INR" => "₹",
            "PKR" => "Rs",
            _ => v.Length <= 4 ? v : "£"
        };
    }
}
