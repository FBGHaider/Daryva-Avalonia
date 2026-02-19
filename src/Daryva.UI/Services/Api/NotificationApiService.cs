using System.Net.Http.Json;

namespace Daryva.Services.Api;

public class NotificationApiService : INotificationApiService
{
    private readonly IApiClient _apiClient;

    public NotificationApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IEnumerable<NotificationRecipientDto>> GetRecipientsAsync(RecipientFilterDto filter)
    {
        var query = BuildRecipientQuery(filter);
        return await _apiClient.HttpClient.GetFromJsonAsync<IEnumerable<NotificationRecipientDto>>($"api/notifications/recipients{query}")
               ?? Array.Empty<NotificationRecipientDto>();
    }

    public async Task<IEnumerable<NotificationTemplateDto>> GetTemplatesAsync(string? channel = null, string? type = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(channel))
            query.Add($"channel={Uri.EscapeDataString(channel)}");
        if (!string.IsNullOrWhiteSpace(type))
            query.Add($"type={Uri.EscapeDataString(type)}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return await _apiClient.HttpClient.GetFromJsonAsync<IEnumerable<NotificationTemplateDto>>($"api/notification-templates{qs}")
               ?? Array.Empty<NotificationTemplateDto>();
    }

    public async Task<NotificationTemplateDto?> GetTemplateByIdAsync(Guid templateId)
    {
        return await _apiClient.HttpClient.GetFromJsonAsync<NotificationTemplateDto>($"api/notification-templates/{templateId}");
    }

    public async Task<IEnumerable<NotificationResponseDto>> GetNotificationsAsync(NotificationFilterDto filter)
    {
        var query = BuildNotificationQuery(filter);
        return await _apiClient.HttpClient.GetFromJsonAsync<IEnumerable<NotificationResponseDto>>($"api/notifications{query}")
               ?? Array.Empty<NotificationResponseDto>();
    }

    public async Task<NotificationResponseDto?> GetNotificationByIdAsync(Guid notificationId)
    {
        return await _apiClient.HttpClient.GetFromJsonAsync<NotificationResponseDto>($"api/notifications/{notificationId}");
    }

    public async Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationRequestDto request)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/notifications", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotificationResponseDto>())!;
    }

    public async Task<bool> SendNotificationAsync(Guid notificationId)
    {
        var response = await _apiClient.HttpClient.PostAsync($"api/notifications/{notificationId}/send", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ActionResponse>();
        return result?.Success ?? false;
    }

    public async Task<bool> SendNotificationWithContentAsync(Guid notificationId, SendNotificationWithContentRequestDto request)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync($"api/notifications/{notificationId}/send-with-content", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ActionResponse>();
        return result?.Success ?? false;
    }

    public async Task<bool> SendBatchAsync(IEnumerable<Guid> notificationIds)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/notifications/send-batch", new SendBatchRequest { NotificationIds = notificationIds.ToList() });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ActionResponse>();
        return result?.Success ?? false;
    }

    public async Task<int> ProcessDueQueueAsync()
    {
        var response = await _apiClient.HttpClient.PostAsync("api/notifications/process-due", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProcessDueResponse>();
        return result?.Sent ?? 0;
    }

    public async Task<bool> CancelNotificationAsync(Guid notificationId)
    {
        var response = await _apiClient.HttpClient.PostAsync($"api/notifications/{notificationId}/cancel", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ActionResponse>();
        return result?.Success ?? false;
    }

    private static string BuildRecipientQuery(RecipientFilterDto filter)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.TargetType))
            query.Add($"targetType={Uri.EscapeDataString(filter.TargetType)}");
        if (filter.TenantId.HasValue)
            query.Add($"tenantId={filter.TenantId.Value}");
        if (filter.HouseId.HasValue)
            query.Add($"houseId={filter.HouseId.Value}");
        if (!string.IsNullOrWhiteSpace(filter.StatusFilter))
            query.Add($"statusFilter={Uri.EscapeDataString(filter.StatusFilter)}");
        if (filter.Month.HasValue)
            query.Add($"month={filter.Month.Value}");
        if (filter.Year.HasValue)
            query.Add($"year={filter.Year.Value}");

        return query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
    }

    private static string BuildNotificationQuery(NotificationFilterDto filter)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query.Add($"status={Uri.EscapeDataString(filter.Status)}");
        if (filter.StartDate.HasValue)
            query.Add($"startDate={Uri.EscapeDataString(filter.StartDate.Value.ToString("o"))}");
        if (filter.EndDate.HasValue)
            query.Add($"endDate={Uri.EscapeDataString(filter.EndDate.Value.ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(filter.Channel))
            query.Add($"channel={Uri.EscapeDataString(filter.Channel)}");
        if (!string.IsNullOrWhiteSpace(filter.Type))
            query.Add($"type={Uri.EscapeDataString(filter.Type)}");
        if (filter.TenantId.HasValue)
            query.Add($"tenantId={filter.TenantId.Value}");
        if (filter.HouseId.HasValue)
            query.Add($"houseId={filter.HouseId.Value}");

        return query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
    }

    private class ActionResponse
    {
        public bool Success { get; set; }
    }

    private class ProcessDueResponse
    {
        public int Sent { get; set; }
    }

    private class SendBatchRequest
    {
        public List<Guid> NotificationIds { get; set; } = new();
    }
}
