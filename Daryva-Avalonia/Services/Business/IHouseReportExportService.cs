using System;
using System.Threading;
using System.Threading.Tasks;
using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IHouseReportExportService
    {
        Task<string> ExportHouseReportAsync(int houseId, DateRange? range, string savePath, CancellationToken ct);
    }
}
