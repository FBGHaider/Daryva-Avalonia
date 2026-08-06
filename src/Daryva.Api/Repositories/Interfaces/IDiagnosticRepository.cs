using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

/// <summary>
/// Dev-only, cross-org raw data access for DiagnosticController. Every method here deliberately
/// ignores the org query filter -- kept as its own repository rather than adding an
/// IgnoreQueryFilters variant to each domain repository, since every other method on e.g.
/// IHouseRepository is implicitly org-scoped by design and bolting a "break tenant isolation"
/// escape hatch onto those interfaces would be a wart on an otherwise-clean contract.
/// </summary>
public interface IDiagnosticRepository
{
    Task<List<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<List<House>> GetHousesAsync(CancellationToken cancellationToken = default);
    Task<List<Tenant>> GetTenantsAsync(CancellationToken cancellationToken = default);
    Task<List<Tenancy>> GetTenanciesAsync(CancellationToken cancellationToken = default);
    Task<List<Expense>> GetExpensesAsync(CancellationToken cancellationToken = default);
    Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<List<RentPayment>> GetRentPaymentsAsync(CancellationToken cancellationToken = default);
    Task<List<DepositPayment>> GetDepositPaymentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Organization navigation included -- for the org-members diagnostic view.</summary>
    Task<List<OrganizationMember>> GetAllMembershipsWithOrganizationAsync(CancellationToken cancellationToken = default);
}
