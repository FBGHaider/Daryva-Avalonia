using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for organisation management (local MVP; can be replaced by SaaS API later).
    /// </summary>
    public interface IOrganisationService
    {
        Task<IReadOnlyList<Organisation>> GetMyOrganisationsAsync(CancellationToken cancellationToken = default);
        Task<Organisation> CreateOrganisationAsync(string name, CancellationToken cancellationToken = default);
        Task RenameOrganisationAsync(Guid orgId, string newName, CancellationToken cancellationToken = default);
        Task SetCurrentOrganisationAsync(Guid orgId, CancellationToken cancellationToken = default);
        Task<Guid?> GetCurrentOrganisationIdAsync(CancellationToken cancellationToken = default);
        Task<Organisation?> GetOrganisationAsync(Guid orgId, CancellationToken cancellationToken = default);
    }
}
