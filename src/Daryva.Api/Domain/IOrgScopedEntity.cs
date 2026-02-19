namespace Daryva.Api.Domain;

/// <summary>
/// Interface for entities that are scoped to an organization (multi-tenant).
/// All org-scoped entities must implement this to enable automatic isolation.
/// </summary>
public interface IOrgScopedEntity
{
    /// <summary>
    /// The organization ID that "owns" this entity.
    /// Used for automatic global query filtering in EF Core.
    /// </summary>
    Guid OrganizationId { get; set; }
}
