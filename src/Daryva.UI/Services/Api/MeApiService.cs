using System.Net.Http.Json;

namespace Daryva.Services.Api;

/// <summary>
/// Calls GET /api/me for SaaS user profile, organisations, and onboarding state.
/// </summary>
public class MeApiService : IMeApiService
{
    private readonly IApiClient _apiClient;

    public MeApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<MeResponseDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/me", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<MeResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
