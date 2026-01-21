using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public class ExportService : IExportService
    {
        public async Task<string> ExportRentDepositLedgerAsync(LedgerExportModel model, CancellationToken ct)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(model.OutputPath)) throw new ArgumentException("OutputPath is required", nameof(model));

            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Rent & Deposit");

                var title = $"{model.MonthYearDisplay} Rent and deposit";

                int currentRow = 1;
                ws.Cell(currentRow, 1).Value = title;
                ws.Range(currentRow, 1, currentRow, 5).Merge();
                ws.Range(currentRow, 1, currentRow, 5).Style
                    .Font.SetBold()
                    .Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Fill.SetBackgroundColor(XLColor.Yellow);
                currentRow += 2;

                // Headers
                var headers = new[] { "Names", "Rent", "Collected by", "Deposit", "Collected by" };
                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(currentRow, i + 1).Value = headers[i];
                }
                var headerRange = ws.Range(currentRow, 1, currentRow, headers.Length);
                headerRange.Style.Font.SetBold();
                headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                headerRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
                currentRow++;

                // Body rows
                var rows = model.Rows ?? Array.Empty<LedgerRowModel>();
                foreach (var row in rows)
                {
                    ws.Cell(currentRow, 1).Value = row.TenantName;
                    ws.Cell(currentRow, 2).Value = row.RentAmount;
                    ws.Cell(currentRow, 3).Value = string.IsNullOrWhiteSpace(row.RentCollectedBy) ? "Nil" : row.RentCollectedBy;
                    ws.Cell(currentRow, 4).Value = row.DepositAmount;
                    ws.Cell(currentRow, 5).Value = string.IsNullOrWhiteSpace(row.DepositCollectedBy) ? "Nil" : row.DepositCollectedBy;

                    ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                    ws.Cell(currentRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                    ws.Cell(currentRow, 3).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                    ws.Cell(currentRow, 5).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                    ws.Range(currentRow, 2, currentRow, 4).Style.NumberFormat.Format = "£ #,##0.00";
                    currentRow++;
                }

                var totals = LedgerTotalsCalculator.CalculateTotals(model);

                // Rent section
                currentRow++;
                ws.Cell(currentRow, 1).Value = "Rent";
                ws.Range(currentRow, 1, currentRow, 5).Merge();
                ws.Range(currentRow, 1, currentRow, 5).Style
                    .Font.SetBold()
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Fill.SetBackgroundColor(XLColor.Yellow);
                currentRow++;

                ws.Cell(currentRow, 1).Value = "Total to be collected";
                ws.Cell(currentRow, 2).Value = totals.TotalRent;
                ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                currentRow++;

                if (model.Collectors != null)
                {
                    foreach (var collector in model.Collectors.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        totals.RentCollectedBy.TryGetValue(collector, out var collected);
                        ws.Cell(currentRow, 1).Value = $"Collected by {collector}";
                        ws.Cell(currentRow, 2).Value = collected;
                        ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                        ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                        currentRow++;
                    }
                }

                ws.Cell(currentRow, 1).Value = "Total Rent collected";
                ws.Cell(currentRow, 2).Value = totals.TotalRent;
                ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                currentRow++;

                ws.Cell(currentRow, 1).Value = "Rent Given to Landlord";
                ws.Cell(currentRow, 2).Value = model.RentGivenToLandlord;
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                currentRow++;

                ws.Cell(currentRow, 1).Value = "Remaining amount";
                ws.Cell(currentRow, 2).Value = totals.RemainingToLandlord;
                ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                currentRow++;

                // Month summary
                currentRow++;
                ws.Cell(currentRow, 1).Value = "Month Summary";
                ws.Range(currentRow, 1, currentRow, 5).Merge();
                ws.Range(currentRow, 1, currentRow, 5).Style
                    .Font.SetBold()
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Fill.SetBackgroundColor(XLColor.Yellow);
                currentRow++;

                var primaryCollector = model.Collectors?.FirstOrDefault() ?? "Collector A";
                var secondaryCollector = model.Collectors?.Skip(1).FirstOrDefault() ?? "Collector B";
                totals.RentCollectedBy.TryGetValue(primaryCollector, out var primaryCollected);
                totals.RentCollectedBy.TryGetValue(secondaryCollector, out var secondaryCollected);
                var owes = secondaryCollected - primaryCollected;

                ws.Cell(currentRow, 1).Value = $"{primaryCollector} Owes {secondaryCollector} in Rent";
                ws.Cell(currentRow, 2).Value = owes;
                ws.Cell(currentRow, 2).Style.NumberFormat.Format = "£ #,##0.00";
                ws.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                currentRow++;

                ws.Columns().AdjustToContents();

                var directory = Path.GetDirectoryName(model.OutputPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                workbook.SaveAs(model.OutputPath);
                return model.OutputPath;
            }, ct);
        }
    }
}
