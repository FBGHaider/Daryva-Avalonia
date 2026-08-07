using System.Text;
using System.Text.Json;

namespace Daryva.Services.Api;

public class TenantApiService : ITenantApiService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TenantApiService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<TenantDto>> GetTenantsAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = includeArchived ? "?includeArchived=true" : "";
            var response = await _apiClient.HttpClient.GetAsync($"api/tenants{query}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get tenants: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tenants = JsonSerializer.Deserialize<List<TenantDto>>(content, _jsonOptions);
            return tenants ?? new List<TenantDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/tenants/{tenantId}", cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to get tenant: {response.StatusCode} - {error}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<TenantDto>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantDto tenant, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(tenant);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PostAsync("api/tenants", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to create tenant: {response.StatusCode} - {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var createdTenant = JsonSerializer.Deserialize<TenantDto>(responseContent, _jsonOptions);
            
            if (createdTenant == null)
                throw new InvalidOperationException("Failed to deserialize created tenant response.");

            return createdTenant;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantDto tenant, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(tenant);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PutAsync($"api/tenants/{tenantId}", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(ApiErrorFormatter.Format("update tenant", response.StatusCode, body));
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedTenant = JsonSerializer.Deserialize<TenantDto>(responseContent, _jsonOptions);
            
            if (updatedTenant == null)
                throw new InvalidOperationException("Failed to deserialize updated tenant response.");

            return updatedTenant;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> ArchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.PostAsync($"api/tenants/{tenantId}/archive", null, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to archive tenant: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> UnarchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.PostAsync($"api/tenants/{tenantId}/unarchive", null, cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to unarchive tenant: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.DeleteAsync($"api/tenants/{tenantId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Failed to delete tenant: {response.StatusCode} - {error}");
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }

    public async Task<bool> InviteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.PostAsync($"api/tenants/{tenantId}/invite", null, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(ApiErrorFormatter.Format("send tenant invite", response.StatusCode, body));
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to connect to API. Please check that the backend is running.", ex);
        }
    }
}
