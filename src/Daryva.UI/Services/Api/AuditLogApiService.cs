using System.Net;
using System.Net.Http.Json;

namespace Daryva.Services.Api;

public class AuditLogApiService : IAuditLogApiService
{
    private readonly IApiClient _apiClient;

    public AuditLogApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<AuditLogListResultDto> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/audit-logs{BuildQueryString(query)}", cancellationToken);

        // [Authorize(Policy = Permissions.Audit.View)] denies Tenants with a bare 403 and no body --
        // surface that as friendly text instead of letting EnsureSuccessStatusCode's generic message through.
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("You don't have permission to view the audit log.");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuditLogListResultDto>(cancellationToken: cancellationToken)
            ?? new AuditLogListResultDto();
    }

    private static string BuildQueryString(AuditLogQuery query)
    {
        var parts = new List<string>
        {
            $"page={query.Page}",
            $"pageSize={query.PageSize}"
        };

        if (!string.IsNullOrWhiteSpace(query.EventType))
            parts.Add($"eventType={Uri.EscapeDataString(query.EventType)}");
        if (query.FromDate.HasValue)
            parts.Add($"fromDate={Uri.EscapeDataString(query.FromDate.Value.ToString("o"))}");
        if (query.ToDate.HasValue)
            parts.Add($"toDate={Uri.EscapeDataString(query.ToDate.Value.ToString("o"))}");

        return "?" + string.Join("&", parts);
    }
}
