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

public class JoinOrganizationResultDto
{
    [JsonPropertyName("organizationId")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("alreadyMember")]
    public bool AlreadyMember { get; set; }
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

    /// <summary>
    /// Delete an organization (Owner only).
    /// </summary>
    Task DeleteOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Join organization by invite token.
    /// </summary>
    Task<JoinOrganizationResultDto> JoinByInviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Join organization by code.
    /// </summary>
    Task<JoinOrganizationResultDto> JoinByCodeAsync(string code, CancellationToken cancellationToken = default);
}
