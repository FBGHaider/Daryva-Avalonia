using System.Threading;
using System.Threading.Tasks;
using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IExportService
    {
        Task<string> ExportRentDepositLedgerAsync(LedgerExportModel model, CancellationToken ct);
    }
}
