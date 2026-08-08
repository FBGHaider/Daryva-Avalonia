using System.Net.Http.Json;
using System.Text.Json;

namespace Daryva.Services.Api;

public class PlatformAdminApiService : IPlatformAdminApiService
{
    private readonly IApiClient _apiClient;

    public PlatformAdminApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<PlatformAdminDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/platform-admins", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to load platform admins.");

        return JsonSerializer.Deserialize<List<PlatformAdminDto>>(body) ?? new List<PlatformAdminDto>();
    }

    public async Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsync($"api/platform-admins/{userId}/revoke", null, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to revoke platform admin access.");
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
