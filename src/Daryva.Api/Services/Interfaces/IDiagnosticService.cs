using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Dev-only diagnostics: cross-org raw data counts/summaries, gated on DevAuth:Enabled.
/// A null return means the gate failed -- caller (DiagnosticController) maps that to 400.
/// </summary>
public interface IDiagnosticService
{
    Task<DiagnosticDataCountsResponse?> GetDataCountsAsync(CancellationToken cancellationToken = default);

    Task<DiagnosticOrgMembersResponse?> GetOrgMembersAsync(CancellationToken cancellationToken = default);
}
