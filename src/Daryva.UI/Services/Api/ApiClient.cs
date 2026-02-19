namespace Daryva.Services.Api;

/// <summary>
/// HTTP client wrapper for Daryva.Api backend.
/// Handles base URL configuration and X-Org-Id header management.
/// </summary>
public class ApiClient : IApiClient
{
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";

    private readonly HttpClient _httpClient;
    private Guid? _currentOrgId;
    private readonly IConfigurationService _configuration;

    public Guid? CurrentOrgId => _currentOrgId;
    public HttpClient HttpClient => _httpClient;

    public ApiClient(IConfigurationService configuration)
    {
        _configuration = configuration;

        var baseAddress = configuration.GetValue("ApiBaseUrl") ?? "http://localhost:5000";
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var persistedOrgId = configuration.GetValue(ApiCurrentOrgIdKey);
        if (Guid.TryParse(persistedOrgId, out var orgId) && orgId != Guid.Empty)
        {
            SetCurrentOrgId(orgId);
        }
    }

    public void SetCurrentOrgId(Guid orgId)
    {
        _currentOrgId = orgId;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Org-Id", orgId.ToString());
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, orgId.ToString());
    }

    public void ClearCurrentOrgId()
    {
        _currentOrgId = null;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, string.Empty);
    }
}
