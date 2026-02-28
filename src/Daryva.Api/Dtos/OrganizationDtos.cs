namespace Daryva.Api.Dtos;

/// <summary>
/// Request payload for creating an organization.
/// Current user becomes the Owner.
/// </summary>
public class CreateOrganizationRequest
{
    /// <summary>
    /// Organization display name (e.g., "John's Property Management").
    /// </summary>
    public required string Name { get; set; }
}

/// <summary>
/// Request payload for updating an organization (e.g. rename). Owner only.
/// </summary>
public class UpdateOrganizationRequest
{
    public string? Name { get; set; }
}

/// <summary>
/// Response payload for an organization.
/// </summary>
public class OrganizationResponse
{
    /// <summary>
    /// Unique identifier for the organization.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Organization display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when organization was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Current user's role in this organization (for context).
    /// </summary>
    public string? CurrentUserRole { get; set; }
}
