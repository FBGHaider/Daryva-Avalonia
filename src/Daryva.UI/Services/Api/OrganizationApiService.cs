using System.Text.Json;

namespace Daryva.Services.Api;

/// <summary>
/// Implementation of organization API service.
/// Communicates with Daryva.Api backend for organization operations.
/// </summary>
public class OrganizationApiService : IOrganizationApiService
{
    private readonly IApiClient _apiClient;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public OrganizationApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<OrganizationDto>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync("api/orgs", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var orgs = JsonSerializer.Deserialize<List<OrganizationDto>>(content, JsonOptions);
            return orgs ?? new List<OrganizationDto>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to fetch organizations: {ex.Message}", ex);
        }
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { name };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _apiClient.HttpClient.PostAsync("api/orgs", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var org = JsonSerializer.Deserialize<OrganizationDto>(responseContent, JsonOptions);
            return org ?? throw new InvalidOperationException("Failed to create organization");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to create organization: {ex.Message}", ex);
        }
    }

    public async Task<OrganizationDto> GetOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/orgs/{orgId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var org = JsonSerializer.Deserialize<OrganizationDto>(content, JsonOptions);
            return org ?? throw new InvalidOperationException("Organization not found");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to fetch organization: {ex.Message}", ex);
        }
    }

    public async Task DeleteOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.DeleteAsync($"api/orgs/{orgId}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to delete organization: {ex.Message}", ex);
        }
    }
}
