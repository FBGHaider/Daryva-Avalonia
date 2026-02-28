using System.Text.Json;
using System.Text;
using Daryva.Services;

namespace Daryva.Services.Api;

/// <summary>
/// Implementation of organization API service.
/// Communicates with Daryva.Api backend for organization operations.
/// </summary>
public class OrganizationApiService : IOrganizationApiService
{
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configuration;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    public OrganizationApiService(IApiClient apiClient, IConfigurationService configuration)
    {
        _apiClient = apiClient;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
        catch (OperationCanceledException ex)
        {
            throw new InvalidOperationException(
                "The request took too long. Check that the API is running and your API URL is correct (see Settings).", ex);
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.Message ?? "";
            if (msg.Contains("connection attempt failed", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("did not properly respond", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("failed to respond", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = _configuration.GetValue("ApiBaseUrl")?.Trim() ?? "(not set)";
                throw new InvalidOperationException(
                    $"Cannot reach the API at {baseUrl}. Check that the API server is running, the address is correct (Settings), and that your firewall or network allows connections to that host and port.",
                    ex);
            }
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

    public async Task<List<OrganizationMemberDto>> GetOrganizationMembersAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync($"api/orgs/{orgId}/members", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return new List<OrganizationMemberDto>();

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var list = JsonSerializer.Deserialize<List<OrganizationMemberDto>>(content, JsonOptions);
        return list ?? new List<OrganizationMemberDto>();
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

    public async Task<JoinOrganizationResultDto> JoinByInviteAsync(string token, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { token });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _apiClient.HttpClient.PostAsync("api/orgs/join/invite", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JoinOrganizationResultDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to join organization by invite.");
    }

    public async Task<JoinOrganizationResultDto> JoinByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { code });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _apiClient.HttpClient.PostAsync("api/orgs/join/code", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<JoinOrganizationResultDto>(body, JsonOptions)
            ?? throw new InvalidOperationException("Failed to join organization by code.");
    }

    public async Task<ImportBackupResultDto> ImportBackupAsync(string backupJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupJson))
            throw new ArgumentException("Backup JSON is empty.", nameof(backupJson));

        using var content = new StringContent(backupJson, Encoding.UTF8, "application/json");
        var response = await _apiClient.HttpClient.PostAsync("api/import", content, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<ImportBackupResultDto>(body, JsonOptions);
        if (result == null)
            throw new InvalidOperationException("Invalid import response.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(result.Message ?? $"Import failed: {response.StatusCode}");
        return result;
    }
}
