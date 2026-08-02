using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IBulkImportService
{
    Task<BulkImportResponse> ImportDataAsync(Guid organizationId, BulkImportRequest request, CancellationToken cancellationToken = default);
}
