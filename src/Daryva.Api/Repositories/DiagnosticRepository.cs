using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class DiagnosticRepository : IDiagnosticRepository
{
    private readonly AppDbContext _dbContext;

    public DiagnosticRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Organization>> GetOrganizationsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Organizations.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<House>> GetHousesAsync(CancellationToken cancellationToken = default)
        => _dbContext.Houses.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<Tenant>> GetTenantsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Tenants.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<Tenancy>> GetTenanciesAsync(CancellationToken cancellationToken = default)
        => _dbContext.Tenancies.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<Expense>> GetExpensesAsync(CancellationToken cancellationToken = default)
        => _dbContext.Expenses.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Documents.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<RentPayment>> GetRentPaymentsAsync(CancellationToken cancellationToken = default)
        => _dbContext.RentPayments.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<DepositPayment>> GetDepositPaymentsAsync(CancellationToken cancellationToken = default)
        => _dbContext.DepositPayments.IgnoreQueryFilters().ToListAsync(cancellationToken);

    public Task<List<OrganizationMember>> GetAllMembershipsWithOrganizationAsync(CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.IgnoreQueryFilters().Include(m => m.Organization).ToListAsync(cancellationToken);
}
