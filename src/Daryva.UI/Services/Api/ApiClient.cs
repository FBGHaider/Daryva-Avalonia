namespace Daryva.Services.Api;

/// <summary>
/// HTTP client wrapper for Daryva.Api backend.
/// Handles base URL configuration and X-Org-Id header management.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private Guid? _currentOrgId;

    public Guid? CurrentOrgId => _currentOrgId;
    public HttpClient HttpClient => _httpClient;

    public ApiClient(IConfigurationService configuration)
    {
        var baseAddress = configuration.GetValue("ApiBaseUrl") ?? "http://localhost:5000";
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public void SetCurrentOrgId(Guid orgId)
    {
        _currentOrgId = orgId;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Org-Id", orgId.ToString());
    }

    public void ClearCurrentOrgId()
    {
        _currentOrgId = null;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
    }
}
