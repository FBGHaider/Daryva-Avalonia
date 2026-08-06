using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class DiagnosticService : IDiagnosticService
{
    private readonly IDiagnosticRepository _diagnosticRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticService> _logger;

    public DiagnosticService(
        IDiagnosticRepository diagnosticRepository,
        IConfiguration configuration,
        ILogger<DiagnosticService> logger)
    {
        _diagnosticRepository = diagnosticRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DiagnosticDataCountsResponse?> GetDataCountsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsDevAuthEnabled())
            return null;

        var organizations = await _diagnosticRepository.GetOrganizationsAsync(cancellationToken);
        var houses = await _diagnosticRepository.GetHousesAsync(cancellationToken);
        var tenants = await _diagnosticRepository.GetTenantsAsync(cancellationToken);
        var tenancies = await _diagnosticRepository.GetTenanciesAsync(cancellationToken);
        var expenses = await _diagnosticRepository.GetExpensesAsync(cancellationToken);
        var documents = await _diagnosticRepository.GetDocumentsAsync(cancellationToken);
        var rentPayments = await _diagnosticRepository.GetRentPaymentsAsync(cancellationToken);
        var depositPayments = await _diagnosticRepository.GetDepositPaymentsAsync(cancellationToken);

        return new DiagnosticDataCountsResponse
        {
            Message = "Raw data counts (bypassing organization filters)",
            Counts = new DiagnosticCounts
            {
                Organizations = organizations.Count,
                Houses = houses.Count,
                Tenants = tenants.Count,
                Tenancies = tenancies.Count,
                Expenses = expenses.Count,
                Documents = documents.Count,
                RentPayments = rentPayments.Count,
                DepositPayments = depositPayments.Count
            },
            Organizations = organizations.Select(o => new DiagnosticOrganizationSummary
            {
                Id = o.Id,
                Name = o.Name,
                CreatedAt = o.CreatedAt
            }).ToList(),
            HouseSummary = houses.GroupBy(h => h.OrganizationId).Select(g => new DiagnosticGroupSummary
            {
                OrganizationId = g.Key,
                Count = g.Count(),
                Names = g.Select(h => h.Name).ToList()
            }).ToList(),
            TenantSummary = tenants.GroupBy(t => t.OrganizationId).Select(g => new DiagnosticGroupSummary
            {
                OrganizationId = g.Key,
                Count = g.Count(),
                Names = g.Select(t => t.FullName).ToList()
            }).ToList()
        };
    }

    public async Task<DiagnosticOrgMembersResponse?> GetOrgMembersAsync(CancellationToken cancellationToken = default)
    {
        if (!IsDevAuthEnabled())
            return null;

        var members = await _diagnosticRepository.GetAllMembershipsWithOrganizationAsync(cancellationToken);

        return new DiagnosticOrgMembersResponse
        {
            Message = "All organization memberships",
            Members = members.Select(m => new DiagnosticMember
            {
                UserId = m.UserId,
                OrganizationId = m.OrganizationId,
                OrganizationName = m.Organization?.Name,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList()
        };
    }

    private bool IsDevAuthEnabled() => _configuration.GetValue<bool>("DevAuth:Enabled");
}
