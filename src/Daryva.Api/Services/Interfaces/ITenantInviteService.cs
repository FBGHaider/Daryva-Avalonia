namespace Daryva.Api.Services.Interfaces;

public interface ITenantInviteService
{
    /// <summary>
    /// Generates and emails a portal invite link to the tenant's own email address.
    /// Throws KeyNotFoundException if no tenant with that id exists in the current org context
    /// (the org-scoped query filter already prevents this from leaking a different org's
    /// tenant), or ArgumentException if the tenant has no email address on file.
    /// </summary>
    Task SendInviteAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
