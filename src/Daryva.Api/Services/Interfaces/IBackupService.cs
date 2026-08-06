using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IBackupService
{
    Task<BulkImportRequest> ExportAsync(CancellationToken cancellationToken = default);
}
