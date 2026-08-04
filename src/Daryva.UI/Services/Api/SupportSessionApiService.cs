using System.Net.Http.Json;
using System.Text.Json;

namespace Daryva.Services.Api;

public class SupportSessionApiService : ISupportSessionApiService
{
    private readonly IApiClient _apiClient;

    public SupportSessionApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<AdminOrganizationListResultDto> GetAllOrganizationsAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"search={Uri.EscapeDataString(search.Trim())}");

        var response = await _apiClient.HttpClient.GetAsync($"api/orgs/all?{string.Join("&", parts)}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to load organizations.");

        return JsonSerializer.Deserialize<AdminOrganizationListResultDto>(body)
            ?? new AdminOrganizationListResultDto();
    }

    public async Task<SupportSessionDto> StartSessionAsync(Guid organizationId, string reason, int? durationMinutes = null, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync(
            "api/support-sessions",
            new { organizationId, reason, durationMinutes },
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to start support session.");

        return JsonSerializer.Deserialize<SupportSessionDto>(body)
            ?? throw new InvalidOperationException("Invalid start-session response.");
    }

    public async Task<SupportSessionDto?> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsync($"api/support-sessions/{sessionId}/end", null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to end support session.");

        return JsonSerializer.Deserialize<SupportSessionDto>(body);
    }

    public async Task<List<SupportSessionDto>> ListSessionsAsync(Guid? organizationId = null, bool includeEnded = false, CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"includeEnded={includeEnded.ToString().ToLowerInvariant()}" };
        if (organizationId.HasValue)
            parts.Add($"organizationId={organizationId.Value}");

        var response = await _apiClient.HttpClient.GetAsync($"api/support-sessions?{string.Join("&", parts)}", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to load support sessions.");

        return JsonSerializer.Deserialize<List<SupportSessionDto>>(body) ?? new List<SupportSessionDto>();
    }

    /// <summary>Parses the API's generic `{ error: "..." }` failure body; null if the body isn't shaped that way.</summary>
    private static string? TryGetErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                return errorProp.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
