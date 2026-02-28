using System.Diagnostics;
using System.Net.Http.Json;
using Daryva.Services;

namespace Daryva.Services.Api;

/// <summary>
/// Calls GET /api/me for SaaS user profile, organisations, and onboarding state.
/// </summary>
public class MeApiService : IMeApiService
{
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configuration;

    public MeApiService(IApiClient apiClient, IConfigurationService configuration)
    {
        _apiClient = apiClient;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<MeResponseDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration.GetValue("ApiBaseUrl")?.Trim() ?? "(not set)";
        var response = await _apiClient.HttpClient.GetAsync("api/me", cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;

#if DEBUG
        Debug.WriteLine($"[MeApi] ApiBaseUrl: {baseUrl}");
        Debug.WriteLine($"[MeApi] GET api/me -> StatusCode: {statusCode}");
#endif

        if (!response.IsSuccessStatusCode)
            return null;

        var me = await response.Content.ReadFromJsonAsync<MeResponseDto>(cancellationToken: cancellationToken).ConfigureAwait(false);

#if DEBUG
        if (me != null)
        {
            var orgCount = me.Organisations?.Count ?? 0;
            var userId = me.User?.Id ?? "(null)";
            var email = me.User?.Email ?? "(null)";
            Debug.WriteLine($"[MeApi] organisations.Count: {orgCount}, user.Id: {userId}, user.Email: {email}");
        }
#endif

        return me;
    }
}
