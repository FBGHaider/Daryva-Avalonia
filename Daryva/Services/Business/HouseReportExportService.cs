using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Daryva.MVVM.Models;
using Daryva.Services.Data;

namespace Daryva.Services.Business
{
    public class HouseReportExportService : IHouseReportExportService
    {
        private readonly IHouseService _houseService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IPaymentService _paymentService;
        private readonly IExpenseService _expenseService;
        private readonly IDocumentRepository _documentRepository;
        private readonly IRentChargeRepository _rentChargeRepository;

        public HouseReportExportService(
            IHouseService houseService,
            ITenancyRepository tenancyRepository,
            IPaymentService paymentService,
            IExpenseService expenseService,
            IDocumentRepository documentRepository,
            IRentChargeRepository rentChargeRepository)
        {
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            _rentChargeRepository = rentChargeRepository ?? throw new ArgumentNullException(nameof(rentChargeRepository));
        }

        public async Task<string> ExportHouseReportAsync(int houseId, DateRange? range, string savePath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(savePath))
                throw new ArgumentException("Save path is required", nameof(savePath));

            return await Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();

                // Load house
                var house = await _houseService.GetHouseByIdAsync(houseId);
                if (house == null)
                    throw new InvalidOperationException($"House with ID {houseId} not found");

                // Load all tenancies for this house
                var tenancies = (await _tenancyRepository.GetTenanciesByHouseIdAsync(houseId)).ToList();

                // Build tenant roster
                var tenantRows = new List<TenantReportRow>();
                var activeTenantsCount = 0;
                DateTime? earliestMoveIn = null;
                DateTime? latestPayment = null;

                foreach (var tenancy in tenancies)
                {
                    var tenant = tenancy.Tenant;
                    if (tenant == null) continue;

                    var isActive = tenancy.MoveOutDate == null;
                    if (isActive) activeTenantsCount++;

                    // Calculate totals
                    var rentPaid = await _paymentService.GetTotalRentPaidForPeriodAsync(tenancy.TenancyId, DateTime.Now.Year, DateTime.Now.Month);
                    var totalRentPaid = 0m;
                    var totalDepositPaid = await _paymentService.GetTotalDepositPaidAsync(tenancy.TenancyId);
                    
                    // Get all rent payments for this tenancy (all-time)
                    var allRentPayments = (await _paymentService.GetTransactionsAsync(null, null, "Rent", houseId, tenant.TenantId, null))
                        .Where(t => t.TenancyId == tenancy.TenancyId)
                        .ToList();
                    totalRentPaid = allRentPayments.Sum(t => t.Amount);
                    var paymentDate = allRentPayments.OrderByDescending(t => t.PaidOn).FirstOrDefault()?.PaidOn;
                    if (paymentDate.HasValue && (!latestPayment.HasValue || paymentDate.Value > latestPayment.Value))
                    {
                        latestPayment = paymentDate;
                    }

                    // Get current month rent status
                    var currentMonthStatus = await _paymentService.GetRentStatusForPeriodAsync(tenancy.TenancyId, DateTime.Now.Year, DateTime.Now.Month);

                    // Check documents
                    var documents = await _documentRepository.GetDocumentsByTenancyIdAsync(tenancy.TenancyId);
                    var hasTenancyAgreement = documents.Any(d => d.Type == "TenancyAgreementSigned" && d.IsActive);
                    var studentLetter = documents.FirstOrDefault(d => d.Type == "StudentConfirmationLetter" && d.IsActive);

                    if (earliestMoveIn == null || tenancy.MoveInDate < earliestMoveIn.Value)
                        earliestMoveIn = tenancy.MoveInDate;

                    tenantRows.Add(new TenantReportRow
                    {
                        FullName = tenant.FullName,
                        Status = isActive ? "Active" : "Ended",
                        MoveInDate = tenancy.MoveInDate,
                        MoveOutDate = tenancy.MoveOutDate,
                        University = tenant.UniversityName,
                        Email = tenant.Email,
                        PhoneNumber = tenant.PhoneNumber,
                        MonthlyRent = tenancy.RentAmountMonthly,
                        DepositRequired = tenancy.DepositAmount,
                        RentPaidToDate = totalRentPaid,
                        DepositPaidToDate = totalDepositPaid,
                        DepositRemaining = tenancy.DepositAmount - totalDepositPaid,
                        CurrentMonthRentStatus = currentMonthStatus,
                        PaymentDueDay = tenancy.PaymentDueDay,
                        LastPaymentDate = allRentPayments.OrderByDescending(t => t.PaidOn).FirstOrDefault()?.PaidOn,
                        HasTenancyAgreement = hasTenancyAgreement,
                        HasStudentLetter = studentLetter != null,
                        StudentLetterExpiry = studentLetter?.ValidTo
                    });
                }

                // Load expenses
                var expenses = (await _expenseService.GetExpensesAsync(houseId, range?.FromDate, range?.ToDate)).ToList();
                var expenseRows = expenses.Select(e => new ExpenseReportRow
                {
                    DateIncurred = e.DateIncurred,
                    Category = e.Category,
                    Vendor = e.Vendor,
                    Notes = e.Notes,
                    Amount = e.Amount,
                    HasReceipt = e.ReceiptDocumentId.HasValue
                }).ToList();

                // Calculate totals
                var totalRentCollected = tenantRows.Sum(t => t.RentPaidToDate);
                var totalDepositCollected = tenantRows.Sum(t => t.DepositPaidToDate);
                var totalExpenses = expenses.Sum(e => e.Amount);

                // Build monthly ledger for current month
                var currentMonth = DateTime.Now;
                var monthlyLedger = new List<MonthlyLedgerRow>();
                foreach (var tenantRow in tenantRows.Where(t => t.Status == "Active"))
                {
                    var tenancy = tenancies.FirstOrDefault(ten => ten.Tenant?.FullName == tenantRow.FullName);
                    if (tenancy == null) continue;

                    // Get rent charge for current month (amount due)
                    var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancy.TenancyId, currentMonth.Year, currentMonth.Month);
                    var rentAmount = rentCharge?.AmountDue ?? tenancy.RentAmountMonthly;
                    
                    // Get collector info from payments for current month
                    var rentPayments = (await _paymentService.GetTransactionsAsync(
                        new DateTime(currentMonth.Year, currentMonth.Month, 1),
                        new DateTime(currentMonth.Year, currentMonth.Month, DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month)),
                        "Rent", houseId, tenancy.TenantId, null))
                        .Where(t => t.TenancyId == tenancy.TenancyId)
                        .ToList();
                    
                    var rentCollector = rentPayments.FirstOrDefault()?.CollectedBy ?? "Nil";
                    
                    // Get deposit payments for current month
                    var depositPayments = (await _paymentService.GetTransactionsAsync(
                        new DateTime(currentMonth.Year, currentMonth.Month, 1),
                        new DateTime(currentMonth.Year, currentMonth.Month, DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month)),
                        "Deposit", houseId, tenancy.TenantId, null))
                        .Where(t => t.TenancyId == tenancy.TenancyId)
                        .ToList();
                    
                    var depositAmount = depositPayments.Sum(t => t.Amount);
                    var depositCollector = depositPayments.FirstOrDefault()?.CollectedBy ?? "Nil";

                    monthlyLedger.Add(new MonthlyLedgerRow
                    {
                        TenantName = tenantRow.FullName,
                        RentAmount = rentAmount,
                        RentCollectedBy = rentCollector,
                        DepositAmount = depositAmount,
                        DepositCollectedBy = depositCollector
                    });
                }

                // Build model
                var model = new HouseReportExportModel
                {
                    HouseId = houseId,
                    HouseAddress = $"{house.AddressLine1}, {house.City}",
                    Postcode = house.Postcode,
                    TotalRooms = house.TotalRooms,
                    GeneratedDate = DateTime.Now,
                    ReportRange = range,
                    OutputPath = savePath,
                    TotalRentCollected = totalRentCollected,
                    TotalDepositCollected = totalDepositCollected,
                    ActiveTenantsCount = activeTenantsCount,
                    TotalExpenses = totalExpenses,
                    EarliestMoveInDate = earliestMoveIn,
                    LatestPaymentDate = latestPayment,
                    Tenants = tenantRows,
                    Expenses = expenseRows,
                    MonthlyLedger = monthlyLedger,
                    MonthYearDisplay = currentMonth.ToString("MMMM yyyy")
                };

                HouseReportCalculator.CalculateTotals(model);

                // Generate Excel
                GenerateExcel(model, ct);

                return savePath;
            }, ct);
        }

        private void GenerateExcel(HouseReportExportModel model, CancellationToken ct)
        {
            using var workbook = new XLWorkbook();
            
            // Create Summary worksheet
            var summaryWs = workbook.Worksheets.Add("Summary");
            WriteSummarySheet(summaryWs, model, ct);

            // Create Tenants & Payments worksheet
            var tenantsWs = workbook.Worksheets.Add("Tenants & Payments");
            WriteTenantsSheet(tenantsWs, model, ct);

            // Create Expenses worksheet
            var expensesWs = workbook.Worksheets.Add("Expenses");
            WriteExpensesSheet(expensesWs, model, ct);

            // Save
            var directory = Path.GetDirectoryName(model.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            workbook.SaveAs(model.OutputPath);
        }

        private void WriteSummarySheet(IXLWorksheet ws, HouseReportExportModel model, CancellationToken ct)
        {
            int row = 1;

            // Title
            ws.Cell(row, 1).Value = $"House Report – {model.HouseAddress}";
            ws.Range(row, 1, row, 8).Merge();
            ws.Range(row, 1, row, 8).Style
                .Font.SetBold()
                .Font.SetFontSize(16)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.Yellow);
            row += 2;

            // Generated date and range
            ws.Cell(row, 1).Value = $"Generated: {model.GeneratedDate:dd/MM/yyyy HH:mm}";
            ws.Cell(row, 2).Value = $"Report range: {(model.ReportRange?.FromDate?.ToString("dd/MM/yyyy") ?? "All time")} to {(model.ReportRange?.ToDate?.ToString("dd/MM/yyyy") ?? "All time")}";
            row += 2;

            // Summary cards
            WriteSummaryCard(ws, row, 1, "Total Rent Collected", model.TotalRentCollected, true);
            WriteSummaryCard(ws, row, 3, "Total Deposit Collected", model.TotalDepositCollected, true);
            WriteSummaryCard(ws, row, 5, "Active Tenants", model.ActiveTenantsCount, false);
            WriteSummaryCard(ws, row, 7, "Total Rooms", model.TotalRooms, false);
            row += 3;

            WriteSummaryCard(ws, row, 1, "Total Expenses", model.TotalExpenses, true);
            WriteSummaryCard(ws, row, 3, "Net Income", model.NetIncome, true);
            WriteSummaryCard(ws, row, 5, "Occupancy Rate", model.OccupancyRate, false, "%");
            row += 3;

            // Additional details
            ws.Cell(row, 1).Value = "Additional Details:";
            ws.Cell(row, 1).Style.Font.SetBold();
            row++;

            ws.Cell(row, 1).Value = "Earliest Move-In:";
            ws.Cell(row, 2).Value = model.EarliestMoveInDate?.ToString("dd/MM/yyyy") ?? "N/A";
            row++;

            ws.Cell(row, 1).Value = "Latest Payment:";
            ws.Cell(row, 2).Value = model.LatestPaymentDate?.ToString("dd/MM/yyyy") ?? "N/A";
            row++;

            ws.Columns().AdjustToContents();
        }

        private void WriteSummaryCard(IXLWorksheet ws, int row, int col, string label, decimal value, bool isCurrency, string suffix = "")
        {
            var range = ws.Range(row, col, row + 1, col + 1);
            range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);
            range.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
            range.Style.Fill.SetBackgroundColor(XLColor.LightGray);

            ws.Cell(row, col).Value = label;
            ws.Cell(row, col).Style.Font.SetBold();
            
            if (isCurrency)
            {
                ws.Cell(row + 1, col).Value = value;
                ws.Cell(row + 1, col).Style.NumberFormat.Format = "£ #,##0.00";
                ws.Cell(row + 1, col).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
            }
            else if (!string.IsNullOrEmpty(suffix))
            {
                ws.Cell(row + 1, col).Value = $"{value:N1}{suffix}";
            }
            else
            {
                ws.Cell(row + 1, col).Value = (int)Math.Round(value);
            }
        }

        private void WriteTenantsSheet(IXLWorksheet ws, HouseReportExportModel model, CancellationToken ct)
        {
            int row = 1;

            // Title
            ws.Cell(row, 1).Value = "Tenants (Active & Historical)";
            ws.Range(row, 1, row, 13).Merge();
            ws.Range(row, 1, row, 13).Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.Yellow);
            row += 2;

            // Headers
            var headers = new[] { "Name", "Status", "Move In", "Move Out", "University", "Email", "Phone", "Rent / Month", "Deposit Required", "Rent Paid to Date", "Deposit Paid to Date", "Deposit Remaining", "Last Payment" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
            }
            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.SetBold();
            headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            headerRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
            row++;

            // Data rows
            foreach (var tenant in model.Tenants)
            {
                ws.Cell(row, 1).Value = tenant.FullName;
                ws.Cell(row, 2).Value = tenant.Status;
                ws.Cell(row, 3).Value = tenant.MoveInDate;
                ws.Cell(row, 4).Value = tenant.MoveOutDate;
                ws.Cell(row, 5).Value = tenant.University ?? "";
                ws.Cell(row, 6).Value = tenant.Email;
                ws.Cell(row, 7).Value = tenant.PhoneNumber;
                ws.Cell(row, 8).Value = tenant.MonthlyRent;
                ws.Cell(row, 9).Value = tenant.DepositRequired;
                ws.Cell(row, 10).Value = tenant.RentPaidToDate;
                ws.Cell(row, 11).Value = tenant.DepositPaidToDate;
                ws.Cell(row, 12).Value = tenant.DepositRemaining;
                ws.Cell(row, 13).Value = tenant.LastPaymentDate;

                // Formatting
                ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 13).Style.DateFormat.Format = "dd/MM/yyyy";
                
                ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                ws.Cell(row, 8).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 9).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 10).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 11).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 12).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                
                ws.Range(row, 8, row, 12).Style.NumberFormat.Format = "£ #,##0.00";
                
                row++;
            }

            // Monthly Ledger section
            row += 2;
            ws.Cell(row, 1).Value = $"{model.MonthYearDisplay} Rent and Deposit Ledger";
            ws.Range(row, 1, row, 5).Merge();
            ws.Range(row, 1, row, 5).Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.Yellow);
            row += 2;

            // Ledger headers
            var ledgerHeaders = new[] { "Names", "Rent", "Collected by", "Deposit", "Collected by" };
            for (int i = 0; i < ledgerHeaders.Length; i++)
            {
                ws.Cell(row, i + 1).Value = ledgerHeaders[i];
            }
            var ledgerHeaderRange = ws.Range(row, 1, row, ledgerHeaders.Length);
            ledgerHeaderRange.Style.Font.SetBold();
            ledgerHeaderRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ledgerHeaderRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
            row++;

            // Ledger data
            foreach (var ledgerRow in model.MonthlyLedger)
            {
                ws.Cell(row, 1).Value = ledgerRow.TenantName;
                ws.Cell(row, 2).Value = ledgerRow.RentAmount;
                ws.Cell(row, 3).Value = ledgerRow.RentCollectedBy ?? "Nil";
                ws.Cell(row, 4).Value = ledgerRow.DepositAmount;
                ws.Cell(row, 5).Value = ledgerRow.DepositCollectedBy ?? "Nil";

                ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 4).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 3).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                ws.Cell(row, 5).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
                ws.Cell(row, 4).Style.NumberFormat.Format = "£ #,##0.00";
                row++;
            }

            // Rent totals
            row += 2;
            ws.Cell(row, 1).Value = "Rent";
            ws.Range(row, 1, row, 5).Merge();
            ws.Range(row, 1, row, 5).Style
                .Font.SetBold()
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.Yellow);
            row++;

            ws.Cell(row, 1).Value = "Total to be collected";
            ws.Cell(row, 2).Value = model.MonthlyLedger.Sum(l => l.RentAmount);
            ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
            ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
            row++;

            foreach (var collector in model.Collectors)
            {
                model.RentCollectedByCollector.TryGetValue(collector, out var collected);
                ws.Cell(row, 1).Value = $"Collected by {collector}";
                ws.Cell(row, 2).Value = collected;
                ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
                row++;
            }

            ws.Cell(row, 1).Value = "Total Rent collected";
            ws.Cell(row, 2).Value = model.MonthlyLedger.Sum(l => l.RentAmount);
            ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
            ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
            row++;

            ws.Cell(row, 1).Value = "Rent Given to Landlord";
            ws.Cell(row, 2).Value = model.RentGivenToLandlord;
            ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
            row++;

            ws.Cell(row, 1).Value = "Remaining amount";
            ws.Cell(row, 2).Value = model.MonthlyLedger.Sum(l => l.RentAmount) - model.RentGivenToLandlord;
            ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
            ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";

            ws.Columns().AdjustToContents();
        }

        private void WriteExpensesSheet(IXLWorksheet ws, HouseReportExportModel model, CancellationToken ct)
        {
            int row = 1;

            // Title
            ws.Cell(row, 1).Value = "Expenses, Bills & Damages";
            ws.Range(row, 1, row, 6).Merge();
            ws.Range(row, 1, row, 6).Style
                .Font.SetBold()
                .Font.SetFontSize(14)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Fill.SetBackgroundColor(XLColor.Yellow);
            row += 2;

            // Headers
            var headers = new[] { "Date", "Category", "Vendor", "Description/Notes", "Amount", "Receipt" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(row, i + 1).Value = headers[i];
            }
            var headerRange = ws.Range(row, 1, row, headers.Length);
            headerRange.Style.Font.SetBold();
            headerRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            headerRange.Style.Fill.SetBackgroundColor(XLColor.LightBlue);
            row++;

            // Data rows
            foreach (var expense in model.Expenses)
            {
                ws.Cell(row, 1).Value = expense.DateIncurred;
                ws.Cell(row, 2).Value = expense.Category;
                ws.Cell(row, 3).Value = expense.Vendor ?? "";
                ws.Cell(row, 4).Value = expense.Notes ?? "";
                ws.Cell(row, 5).Value = expense.Amount;
                ws.Cell(row, 6).Value = expense.HasReceipt ? "Yes" : "No";

                ws.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGoldenrodYellow);
                ws.Cell(row, 5).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 5).Style.NumberFormat.Format = "£ #,##0.00";
                row++;
            }

            // Totals by category
            row += 2;
            ws.Cell(row, 1).Value = "Totals by Category:";
            ws.Cell(row, 1).Style.Font.SetBold();
            row++;

            foreach (var category in model.ExpensesByCategory.OrderByDescending(kvp => kvp.Value))
            {
                ws.Cell(row, 1).Value = category.Key;
                ws.Cell(row, 2).Value = category.Value;
                ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";
                row++;
            }

            // Grand total
            row++;
            ws.Cell(row, 1).Value = "Grand Total Expenses";
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 2).Value = model.TotalExpenses;
            ws.Cell(row, 2).Style.Font.SetBold();
            ws.Cell(row, 2).Style.Fill.SetBackgroundColor(XLColor.LightGreen);
            ws.Cell(row, 2).Style.NumberFormat.Format = "£ #,##0.00";

            ws.Columns().AdjustToContents();
        }
    }
}
