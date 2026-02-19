using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>
/// Organization data transfer object from API.
/// </summary>
public class OrganizationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("currentUserRole")]
    public string CurrentUserRole { get; set; } = string.Empty;
}

/// <summary>
/// Service for organization-related API operations.
/// </summary>
public interface IOrganizationApiService
{
    /// <summary>
    /// Get all organizations the current user belongs to.
    /// </summary>
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new organization. Current user becomes the Owner.
    /// </summary>
    Task<OrganizationDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific organization by ID (if user is member).
    /// </summary>
    Task<OrganizationDto> GetOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default);
}
