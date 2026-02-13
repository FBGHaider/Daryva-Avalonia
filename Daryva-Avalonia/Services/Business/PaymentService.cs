using System;
using System.Collections.Generic;
using System.Linq;
using Daryva.MVVM.Models;
using Daryva.MVVM.ViewModels;
using Daryva.Services.Data;

namespace Daryva.Services.Business
{
    public class PaymentService : IPaymentService
    {
        private readonly IDepositPaymentRepository _depositPaymentRepository;
        private readonly IDepositReturnRepository _depositReturnRepository;
        private readonly IRentChargeRepository _rentChargeRepository;
        private readonly IRentPaymentRepository _rentPaymentRepository;
        private readonly ITenancyRepository _tenancyRepository;

        public PaymentService(
            IDepositPaymentRepository depositPaymentRepository,
            IDepositReturnRepository depositReturnRepository,
            IRentChargeRepository rentChargeRepository,
            IRentPaymentRepository rentPaymentRepository,
            ITenancyRepository tenancyRepository)
        {
            _depositPaymentRepository = depositPaymentRepository;
            _depositReturnRepository = depositReturnRepository;
            _rentChargeRepository = rentChargeRepository;
            _rentPaymentRepository = rentPaymentRepository;
            _tenancyRepository = tenancyRepository;
        }

        public async Task RecordPaymentAsync(int tenancyId, decimal depositAmount, decimal rentAmount, int rentYear, int rentMonth, DateTime paymentDate, string method, string? reference, string? notes, string? collectedBy = null, bool useDepositForRent = false)
        {
            // Get tenancy to get rent amount and payment due day
            var tenancy = await _tenancyRepository.GetTenancyByIdAsync(tenancyId);
            if (tenancy == null)
                throw new InvalidOperationException($"Tenancy {tenancyId} not found");

            // When "use deposit for rent" is selected we only record rent (paid from existing deposit); no new deposit payment
            if (!useDepositForRent)
            {
                // Record deposit payment if amount > 0
                if (depositAmount > 0)
                {
                    var depositPayment = new DepositPayment
                    {
                        TenancyId = tenancyId,
                        PaidOn = paymentDate,
                        AmountPaid = depositAmount,
                        Method = method,
                        Reference = reference,
                        Notes = notes,
                        CollectedBy = collectedBy
                    };
                    await _depositPaymentRepository.CreateDepositPaymentAsync(depositPayment);
                }
            }

            // Record rent payment if amount > 0
            if (rentAmount > 0)
            {
                // Ensure rent charge exists
                var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancyId, rentYear, rentMonth);
                
                if (rentCharge == null)
                {
                    // Create rent charge
                    var dueDate = new DateTime(rentYear, rentMonth, Math.Min((int)tenancy.PaymentDueDay, 28));
                    rentCharge = new RentCharge
                    {
                        TenancyId = tenancyId,
                        PeriodYear = rentYear,
                        PeriodMonth = rentMonth,
                        AmountDue = tenancy.RentAmountMonthly,
                        DueDate = dueDate,
                        Status = "Pending" // DB expects Pending (not Unpaid)
                    };
                    rentCharge.RentChargeId = await _rentChargeRepository.CreateRentChargeAsync(rentCharge);
                }

                // Create rent payment (PaidFromDeposit = true when using deposit for this month's rent)
                var rentPayment = new RentPayment
                {
                    TenancyId = tenancyId,
                    RentChargeId = rentCharge.RentChargeId,
                    PaidOn = paymentDate,
                    AmountPaid = rentAmount,
                    Method = method,
                    Reference = reference,
                    Notes = notes,
                    CollectedBy = collectedBy,
                    PaidFromDeposit = useDepositForRent
                };
                await _rentPaymentRepository.CreateRentPaymentAsync(rentPayment);

                // Update rent charge status
                var totalPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(rentCharge.RentChargeId);
                string newStatus;
                if (totalPaid >= rentCharge.AmountDue)
                {
                    newStatus = "Paid";
                }
                else if (totalPaid > 0)
                {
                    // Partially paid - check if overdue
                    if (rentCharge.DueDate < DateTime.Today)
                        newStatus = "Overdue"; // Part-paid but overdue
                    else
                        newStatus = "PartPaid";
                }
                else
                {
                    // Unpaid - check if overdue
                    if (rentCharge.DueDate < DateTime.Today)
                        newStatus = "Overdue";
                    else
                        newStatus = "Unpaid";
                }

                await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, ToDbStatus(newStatus));
            }
            
            // Notify dashboard to refresh after payment is recorded
            DashboardViewModel.NotifyPaymentDataChanged();
        }

        public async Task<decimal> GetTotalDepositPaidAsync(int tenancyId)
        {
            return await _depositPaymentRepository.GetTotalDepositPaidAsync(tenancyId);
        }

        public async Task<decimal> GetTotalRentPaidForPeriodAsync(int tenancyId, int year, int month)
        {
            var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancyId, year, month);
            if (rentCharge == null)
                return 0;

            return await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(rentCharge.RentChargeId);
        }

        public async Task<string> GetDepositStatusAsync(int tenancyId, decimal depositRequired)
        {
            var totalPaid = await GetTotalDepositPaidAsync(tenancyId);
            if (totalPaid >= depositRequired)
                return "Paid";
            else if (totalPaid > 0)
                return "PartPaid";
            else
                return "Unpaid";
        }

        public async Task<string> GetRentStatusForPeriodAsync(int tenancyId, int year, int month)
        {
            var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancyId, year, month);
            if (rentCharge == null)
                return "Unpaid";

            return rentCharge.Status;
        }

        public async Task<IEnumerable<RentLedgerRowViewModel>> GetRentLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null)
        {
            List<RentPayment> allRentPaymentsInPeriod;
            try
            {
                allRentPaymentsInPeriod = (await _rentPaymentRepository.GetRentPaymentsInPeriodAsync(year, month)).ToList();
            }
            catch
            {
                var periodStart = new DateTime(year, month, 1);
                var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                var all = (await _rentPaymentRepository.GetAllRentPaymentsAsync(periodStart, periodEnd, null)).ToList();
                allRentPaymentsInPeriod = all.Where(rp => rp.PaidOn.Year == year && rp.PaidOn.Month == month).ToList();
            }
            var rentPaidByTenancyId = allRentPaymentsInPeriod
                .GroupBy(rp => rp.TenancyId)
                .ToDictionary(g => g.Key, g => (Sum: g.Sum(rp => rp.AmountPaid), List: g.ToList()));

            Dictionary<int, (decimal Sum, List<RentPayment> List)> rentPaidByTenantId = new Dictionary<int, (decimal, List<RentPayment>)>();
            var periodTenancies = (await _tenancyRepository.GetTenanciesActiveInPeriodAsync(year, month)).ToList();
            try
            {
                var tenantIdByTenancyId = periodTenancies.Where(t => t != null).GroupBy(t => t!.TenancyId).ToDictionary(g => g.Key, g => g.First().TenantId);
                foreach (var rp in allRentPaymentsInPeriod)
                {
                    if (!tenantIdByTenancyId.ContainsKey(rp.TenancyId))
                    {
                        var t = await _tenancyRepository.GetTenancyByIdAsync(rp.TenancyId);
                        if (t != null) tenantIdByTenancyId[rp.TenancyId] = t.TenantId;
                    }
                }
                rentPaidByTenantId = allRentPaymentsInPeriod
                    .Where(rp => tenantIdByTenancyId.TryGetValue(rp.TenancyId, out var tid) && tid != 0)
                    .GroupBy(rp => tenantIdByTenancyId[rp.TenancyId])
                    .ToDictionary(g => g.Key, g => (Sum: g.Sum(rp => rp.AmountPaid), List: g.ToList()));
            }
            catch
            {
                // If TenantId fallback setup fails, continue without it; ledger still uses rentPaidByTenancyId
            }

            var charges = (await _rentChargeRepository.GetRentChargesForPeriodAsync(year, month)).ToList();
            var ledgerRows = new List<RentLedgerRowViewModel>();
            var periodTenanciesByTenancyId = periodTenancies
                .Where(t => t != null)
                .GroupBy(t => t!.TenancyId)
                .ToDictionary(g => g.Key, g => g.First()!);

            foreach (var charge in charges)
            {
                try
                {
                    // Resolve tenancy: prefer period list (includes ended/archived); fallback to load by ID so we never drop paid history
                    if (!periodTenanciesByTenancyId.TryGetValue(charge.TenancyId, out var tenancy) || tenancy == null)
                        tenancy = await _tenancyRepository.GetTenancyByIdAsync(charge.TenancyId);
                    if (tenancy == null)
                        continue;

                    // Exclude ended tenancies only for the move-out month and later (tenant left, no current-month row). Past months stay for history.
                    // Past months (selected period before move-out) must still show for history.
                    if (tenancy.MoveOutDate.HasValue)
                    {
                        var moveOut = tenancy.MoveOutDate.Value;
                        int selectedPeriod = year * 12 + month;
                        int moveOutPeriod = moveOut.Year * 12 + moveOut.Month;
                        if (selectedPeriod >= moveOutPeriod)
                            continue;
                    }

                    // Don't show rent for months before rent start (e.g. move-in 01 Feb + "rent start same month" => no row for January)
                    int rentStartYear = tenancy.RentStartYear ?? tenancy.MoveInDate.Year;
                    int rentStartMonth = tenancy.RentStartMonth ?? tenancy.MoveInDate.Month;
                    int periodNum = year * 12 + month;
                    int firstRentPeriodNum = rentStartYear * 12 + rentStartMonth;
                    if (periodNum < firstRentPeriodNum)
                        continue;

                    // Apply house filter
                    if (houseId.HasValue && tenancy.HouseId != houseId.Value)
                        continue;

                    // Apply search filter (null-safe)
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var searchLower = searchTerm.ToLower();
                        var nameMatch = (tenancy.Tenant?.FullName ?? "").ToLower().Contains(searchLower);
                        var addressMatch = (tenancy.House?.AddressLine1 ?? "").ToLower().Contains(searchLower);
                        if (!nameMatch && !addressMatch)
                            continue;
                    }

                    decimal amountDue = charge.AmountDue;
                    var dueDate = charge.DueDate;
                    // Use payments linked to this charge (by RentChargeId) so rent paid in a different calendar month
                    // (e.g. February rent paid in March) is still counted for this period. Fixes dashboard showing
                    // "Rent due this month" when all tenants have paid.
                    decimal amountPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(charge.RentChargeId);
                    var rentPaymentsList = (await _rentPaymentRepository.GetRentPaymentsByChargeIdAsync(charge.RentChargeId)).ToList();
                    // Fallback for legacy payments not linked to a charge: use PaidOn-in-period totals
                    if (amountPaid == 0 && rentPaidByTenancyId.TryGetValue(tenancy.TenancyId, out var paidByTenancy))
                    {
                        amountPaid = paidByTenancy.Sum;
                        if (paidByTenancy.List.Count > 0) rentPaymentsList = paidByTenancy.List;
                    }
                    else if (amountPaid == 0 && rentPaidByTenantId.TryGetValue(tenancy.TenantId, out var paidByTenant))
                    {
                        amountPaid = paidByTenant.Sum;
                        if (paidByTenant.List.Count > 0) rentPaymentsList = paidByTenant.List;
                    }
                    var rentPayments = (Sum: amountPaid, List: rentPaymentsList);

                    // Calculate status
                    string status;
                    if (amountPaid >= amountDue)
                        status = "Paid";
                    else if (amountPaid > 0)
                        status = "PartPaid";
                    else if (dueDate < DateTime.Today)
                        status = "Overdue";
                    else
                        status = "Unpaid";

                    // Apply status filter
                    if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                    {
                        string normalizedFilter = statusFilter.Replace("-", "");
                        string normalizedStatus = status.Replace("-", "");
                        if (normalizedFilter.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "Unpaid")
                            continue;
                        if (normalizedFilter.Equals("Partpaid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "PartPaid")
                            continue;
                        if (normalizedFilter.Equals("Paid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "Paid")
                            continue;
                        if (normalizedFilter.Equals("Overdue", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "Overdue")
                            continue;
                    }

                    var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                    var depositRemaining = Math.Max(0, tenancy.DepositAmount - depositPaid);
                    var payments = rentPayments.List.Select(rp => new PaymentDetailViewModel
                    {
                        PaidOn = rp.PaidOn,
                        Amount = rp.AmountPaid,
                        Method = rp.Method ?? "",
                        Reference = rp.Reference ?? "",
                        Notes = rp.Notes ?? "",
                        CollectedBy = rp.CollectedBy ?? ""
                    }).ToList();

                    ledgerRows.Add(new RentLedgerRowViewModel
                    {
                        TenancyId = tenancy.TenancyId,
                        TenantId = tenancy.TenantId,
                        HouseId = tenancy.HouseId,
                        HouseAddress = $"{tenancy.House?.AddressLine1 ?? ""}, {tenancy.House?.City ?? ""}".Trim(',', ' '),
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        DueDate = dueDate,
                        AmountDue = amountDue,
                        AmountPaid = amountPaid,
                        Status = status,
                        DepositRemaining = depositRemaining,
                        PaymentsForThisMonth = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                    });
                }
                catch
                {
                    // Skip this charge/tenancy if any null ref or error (e.g. missing data after tenant removal or recovery)
                }
            }

            // Deduplicate: only one row per tenancy per period (avoids duplicate rows from duplicate RentCharge records)
            ledgerRows = ledgerRows
                .GroupBy(r => r.TenancyId)
                .Select(g => g.OrderByDescending(r => r.AmountPaid).ThenByDescending(r => r.DueDate).First())
                .ToList();

            // Ensure active tenancies without a charge for this month still get a row (e.g. current month not yet charged)
            var tenancyIdsWithRow = ledgerRows.Select(r => r.TenancyId).ToHashSet();
            var activeTenancies = (await _tenancyRepository.GetActiveTenanciesAsync()).ToList();
            var rentStartByTenancy = activeTenancies.Count > 0
                ? await _tenancyRepository.GetRentStartByTenancyIdsAsync(activeTenancies.Select(t => t.TenancyId))
                : new Dictionary<int, (int Month, int Year)>();
            var periodStartDate = new DateTime(year, month, 1);
            int selectedPeriodNum = year * 12 + month;

            foreach (var tenancy in activeTenancies)
            {
                try
                {
                    if (tenancy == null || tenancyIdsWithRow.Contains(tenancy.TenancyId)) continue;

                    var rentStartYear = rentStartByTenancy.TryGetValue(tenancy.TenancyId, out var rs)
                        ? rs.Year
                        : tenancy.MoveInDate.Year;
                    var rentStartMonth = rentStartByTenancy.TryGetValue(tenancy.TenancyId, out var rsM)
                        ? rsM.Month
                        : tenancy.MoveInDate.Month;
                    var firstRentPeriodStart = new DateTime(rentStartYear, rentStartMonth, 1);
                    if (periodStartDate < firstRentPeriodStart) continue;

                    if (houseId.HasValue && tenancy.HouseId != houseId.Value) continue;
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var searchLower = searchTerm.ToLower();
                        if (!(tenancy.Tenant?.FullName ?? "").ToLower().Contains(searchLower) &&
                            !(tenancy.House?.AddressLine1 ?? "").ToLower().Contains(searchLower))
                            continue;
                    }

                    var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancy.TenancyId, year, month);
                    decimal amountDue = rentCharge?.AmountDue ?? tenancy.RentAmountMonthly;
                    var dueDate = rentCharge?.DueDate ?? new DateTime(year, month, Math.Min((int)tenancy.PaymentDueDay, 28));
                    var (amtPaid, rpsList) = rentPaidByTenancyId.TryGetValue(tenancy.TenancyId, out var p) ? (p.Sum, p.List)
                        : rentPaidByTenantId.TryGetValue(tenancy.TenantId, out var byT) ? (byT.Sum, byT.List)
                        : (0m, new List<RentPayment>());
                    var payments = rpsList.Select(rp => new PaymentDetailViewModel
                    {
                        PaidOn = rp.PaidOn,
                        Amount = rp.AmountPaid,
                        Method = rp.Method ?? "",
                        Reference = rp.Reference ?? "",
                        Notes = rp.Notes ?? "",
                        CollectedBy = rp.CollectedBy ?? ""
                    }).ToList();
                    string status;
                    if (amtPaid >= amountDue) status = "Paid";
                    else if (amtPaid > 0) status = "PartPaid";
                    else if (dueDate < DateTime.Today) status = "Overdue";
                    else status = "Unpaid";
                    if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                    {
                        var nf = statusFilter.Replace("-", "");
                        var ns = status.Replace("-", "");
                        if (nf.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && ns != "Unpaid") continue;
                        if (nf.Equals("Partpaid", StringComparison.OrdinalIgnoreCase) && ns != "PartPaid") continue;
                        if (nf.Equals("Paid", StringComparison.OrdinalIgnoreCase) && ns != "Paid") continue;
                        if (nf.Equals("Overdue", StringComparison.OrdinalIgnoreCase) && ns != "Overdue") continue;
                    }
                    var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                    var depositRemaining = Math.Max(0, tenancy.DepositAmount - depositPaid);
                    ledgerRows.Add(new RentLedgerRowViewModel
                    {
                        TenancyId = tenancy.TenancyId,
                        TenantId = tenancy.TenantId,
                        HouseId = tenancy.HouseId,
                        HouseAddress = $"{tenancy.House?.AddressLine1 ?? ""}, {tenancy.House?.City ?? ""}".Trim(',', ' '),
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        DueDate = dueDate,
                        AmountDue = amountDue,
                        AmountPaid = amtPaid,
                        Status = status,
                        DepositRemaining = depositRemaining,
                        PaymentsForThisMonth = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                    });
                }
                catch
                {
                    // Skip this tenancy if any null ref or error (e.g. recovered tenant with incomplete data)
                }
            }

            // Past months history: include ended tenancies for selected period when period is before their move-out (so history remains)
            foreach (var tenancy in periodTenancies)
            {
                try
                {
                    if (tenancy == null || !tenancy.MoveOutDate.HasValue || tenancyIdsWithRow.Contains(tenancy.TenancyId))
                        continue;
                    int moveOutPeriodNum = tenancy.MoveOutDate.Value.Year * 12 + tenancy.MoveOutDate.Value.Month;
                    if (selectedPeriodNum >= moveOutPeriodNum)
                        continue;
                    int firstRentPast = (tenancy.RentStartYear ?? tenancy.MoveInDate.Year) * 12 + (tenancy.RentStartMonth ?? tenancy.MoveInDate.Month);
                    if (selectedPeriodNum < firstRentPast)
                        continue;
                    if (houseId.HasValue && tenancy.HouseId != houseId.Value) continue;
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var searchLower = searchTerm.ToLower();
                        if (!(tenancy.Tenant?.FullName ?? "").ToLower().Contains(searchLower) &&
                            !(tenancy.House?.AddressLine1 ?? "").ToLower().Contains(searchLower))
                            continue;
                    }
                    var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancy.TenancyId, year, month);
                    decimal amountDue = rentCharge?.AmountDue ?? tenancy.RentAmountMonthly;
                    var dueDate = rentCharge?.DueDate ?? new DateTime(year, month, Math.Min((int)tenancy.PaymentDueDay, 28));
                    var (amountPaidPast, rpsPast) = rentPaidByTenancyId.TryGetValue(tenancy.TenancyId, out var p2) ? (p2.Sum, p2.List)
                        : rentPaidByTenantId.TryGetValue(tenancy.TenantId, out var byT2) ? (byT2.Sum, byT2.List)
                        : (0m, new List<RentPayment>());
                    var payments = rpsPast.Select(rp => new PaymentDetailViewModel
                    {
                        PaidOn = rp.PaidOn,
                        Amount = rp.AmountPaid,
                        Method = rp.Method ?? "",
                        Reference = rp.Reference ?? "",
                        Notes = rp.Notes ?? "",
                        CollectedBy = rp.CollectedBy ?? ""
                    }).ToList();
                    string status;
                    if (amountPaidPast >= amountDue) status = "Paid";
                    else if (amountPaidPast > 0) status = "PartPaid";
                    else if (dueDate < DateTime.Today) status = "Overdue";
                    else status = "Unpaid";
                    if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                    {
                        var nf = statusFilter.Replace("-", "");
                        var ns = status.Replace("-", "");
                        if (nf.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && ns != "Unpaid") continue;
                        if (nf.Equals("Partpaid", StringComparison.OrdinalIgnoreCase) && ns != "PartPaid") continue;
                        if (nf.Equals("Paid", StringComparison.OrdinalIgnoreCase) && ns != "Paid") continue;
                        if (nf.Equals("Overdue", StringComparison.OrdinalIgnoreCase) && ns != "Overdue") continue;
                    }
                    var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                    var depositRemaining = Math.Max(0, tenancy.DepositAmount - depositPaid);
                    ledgerRows.Add(new RentLedgerRowViewModel
                    {
                        TenancyId = tenancy.TenancyId,
                        TenantId = tenancy.TenantId,
                        HouseId = tenancy.HouseId,
                        HouseAddress = $"{tenancy.House?.AddressLine1 ?? ""}, {tenancy.House?.City ?? ""}".Trim(',', ' '),
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        DueDate = dueDate,
                        AmountDue = amountDue,
                        AmountPaid = amountPaidPast,
                        Status = status,
                        DepositRemaining = depositRemaining,
                        PaymentsForThisMonth = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                    });
                }
                catch
                {
                    // Skip this tenancy if any null ref or error
                }
            }

            // Ensure every tenancy active in this period has a row (fixes empty ledger when no charges exist for the month)
            tenancyIdsWithRow = ledgerRows.Select(r => r.TenancyId).ToHashSet();
            foreach (var tenancy in periodTenancies)
            {
                try
                {
                    if (tenancy == null || tenancyIdsWithRow.Contains(tenancy.TenancyId)) continue;
                    // Do not show left tenants for the leave month or any future month (only past months for history)
                    if (tenancy.MoveOutDate.HasValue)
                    {
                        int selNum = year * 12 + month;
                        int moveNum = tenancy.MoveOutDate.Value.Year * 12 + tenancy.MoveOutDate.Value.Month;
                        if (selNum >= moveNum) continue;
                    }
                    // Don't show row for months before rent start (e.g. move-in Feb + rent start same month => no January row)
                    int firstRentFourth = (tenancy.RentStartYear ?? tenancy.MoveInDate.Year) * 12 + (tenancy.RentStartMonth ?? tenancy.MoveInDate.Month);
                    if (selectedPeriodNum < firstRentFourth)
                        continue;
                    if (houseId.HasValue && tenancy.HouseId != houseId.Value) continue;
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var searchLower = searchTerm.ToLower();
                        if (!(tenancy.Tenant?.FullName ?? "").ToLower().Contains(searchLower) &&
                            !(tenancy.House?.AddressLine1 ?? "").ToLower().Contains(searchLower))
                            continue;
                    }
                    var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancy.TenancyId, year, month);
                    decimal amountDue = rentCharge?.AmountDue ?? tenancy.RentAmountMonthly;
                    var dueDate = rentCharge?.DueDate ?? new DateTime(year, month, Math.Min((int)tenancy.PaymentDueDay, 28));
                    var (amt, rps) = rentPaidByTenancyId.TryGetValue(tenancy.TenancyId, out var px) ? (px.Sum, px.List)
                        : rentPaidByTenantId.TryGetValue(tenancy.TenantId, out var bx) ? (bx.Sum, bx.List)
                        : (0m, new List<RentPayment>());
                    var payments = rps.Select(rp => new PaymentDetailViewModel
                    {
                        PaidOn = rp.PaidOn,
                        Amount = rp.AmountPaid,
                        Method = rp.Method ?? "",
                        Reference = rp.Reference ?? "",
                        Notes = rp.Notes ?? "",
                        CollectedBy = rp.CollectedBy ?? ""
                    }).ToList();
                    string status = amt >= amountDue ? "Paid" : amt > 0 ? "PartPaid" : dueDate < DateTime.Today ? "Overdue" : "Unpaid";
                    if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                    {
                        var nf = statusFilter.Replace("-", "");
                        var ns = status.Replace("-", "");
                        if (nf.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && ns != "Unpaid") continue;
                        if (nf.Equals("Partpaid", StringComparison.OrdinalIgnoreCase) && ns != "PartPaid") continue;
                        if (nf.Equals("Paid", StringComparison.OrdinalIgnoreCase) && ns != "Paid") continue;
                        if (nf.Equals("Overdue", StringComparison.OrdinalIgnoreCase) && ns != "Overdue") continue;
                    }
                    var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                    var depositRemaining = Math.Max(0, tenancy.DepositAmount - depositPaid);
                    ledgerRows.Add(new RentLedgerRowViewModel
                    {
                        TenancyId = tenancy.TenancyId,
                        TenantId = tenancy.TenantId,
                        HouseId = tenancy.HouseId,
                        HouseAddress = $"{tenancy.House?.AddressLine1 ?? ""}, {tenancy.House?.City ?? ""}".Trim(',', ' '),
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        DueDate = dueDate,
                        AmountDue = amountDue,
                        AmountPaid = amt,
                        Status = status,
                        DepositRemaining = depositRemaining,
                        PaymentsForThisMonth = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                    });
                }
                catch { /* skip */ }
            }

            // Final dedupe: one row per tenant per property per period (same person can have two tenancy records; show only one row, prefer the one with payments)
            ledgerRows = ledgerRows
                .GroupBy(r => new { r.TenantId, r.HouseAddress })
                .Select(g => g.OrderByDescending(r => r.AmountPaid).ThenByDescending(r => r.DueDate).First())
                .ToList();

            return ledgerRows.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName);
        }

        public async Task<IEnumerable<DepositLedgerRowViewModel>> GetDepositLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null)
        {
            // Include tenancies that were active during this month (keeps history for removed tenants)
            var periodTenancies = (await _tenancyRepository.GetTenanciesActiveInPeriodAsync(year, month)).ToList();
            var periodTenancyIds = periodTenancies.Select(t => t.TenancyId).ToHashSet();
            // Also include any tenancy that has a deposit payment (so tenants with transactions always show in deposit ledger)
            var tenancyIdsWithDeposits = (await _depositPaymentRepository.GetTenancyIdsWithDepositPaymentsAsync()).ToList();
            foreach (var tid in tenancyIdsWithDeposits)
            {
                if (periodTenancyIds.Contains(tid)) continue;
                var t = await _tenancyRepository.GetTenancyByIdAsync(tid);
                if (t != null && t.MoveInDate != default)
                {
                    periodTenancies.Add(t);
                    periodTenancyIds.Add(tid);
                }
            }
            var withMoveIn = periodTenancies.Where(t => t != null && t.MoveInDate != default).ToList();
            // Group by (TenantId, HouseId) - deposit is paid once per tenant per house, so we show one row per group
            var groups = withMoveIn
                .GroupBy(t => new { t.TenantId, t.HouseId })
                .ToList();

            var periodStartDate = new DateTime(year, month, 1);
            int selectedPeriodNum = year * 12 + month;
            var allTenancyIdsInGroups = withMoveIn.Select(t => t.TenancyId).Distinct().ToList();
            var depositReturnsByTenancy = await _depositReturnRepository.GetByTenancyIdsAsync(allTenancyIdsInGroups);
            var ledgerRows = new List<DepositLedgerRowViewModel>();

            foreach (var group in groups)
            {
                try
                {
                    var tenanciesInGroup = group.ToList();
                    if (tenanciesInGroup.Count == 0) continue;
                    // Prefer active tenancy for the row (so Record Payment uses the right one), else latest MoveInDate
                    var displayTenancy = tenanciesInGroup
                        .OrderByDescending(t => string.Equals(t.Status, "Active", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                        .ThenByDescending(t => t.MoveInDate)
                        .First();
                    if (displayTenancy == null) continue;

                    // Skip if deposit was returned and we're viewing the return month or later (remove row after paid deposit date)
                    bool depositReturnedForThisPeriod = false;
                    foreach (var t in tenanciesInGroup)
                    {
                        if (depositReturnsByTenancy.TryGetValue(t.TenancyId, out var ret))
                        {
                            int returnPeriod = ret.ReturnedDate.Year * 12 + ret.ReturnedDate.Month;
                            if (selectedPeriodNum >= returnPeriod)
                            {
                                depositReturnedForThisPeriod = true;
                                break;
                            }
                        }
                    }
                    if (depositReturnedForThisPeriod) continue;

                    // Skip if move-in is after the selected month
                    var moveInMonthStart = new DateTime(displayTenancy.MoveInDate.Year, displayTenancy.MoveInDate.Month, 1);
                    if (periodStartDate < moveInMonthStart)
                        continue;
                    // For deposit ledger: show ended tenancies too (they have deposit history); only exclude when viewing a month before they moved in

                    if (houseId.HasValue && displayTenancy.HouseId != houseId.Value)
                        continue;

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var searchLower = searchTerm.ToLower();
                        var nameMatch = (displayTenancy.Tenant?.FullName ?? "").ToLower().Contains(searchLower);
                        var addressMatch = (displayTenancy.House?.AddressLine1 ?? "").ToLower().Contains(searchLower);
                        if (!nameMatch && !addressMatch)
                            continue;
                    }

                    // Deposit is paid once per tenant per house: sum across ALL tenancies in this group
                    decimal depositPaid = 0;
                    var allPaymentsForRow = new List<PaymentDetailViewModel>();
                    foreach (var t in tenanciesInGroup)
                    {
                        depositPaid += await GetTotalDepositPaidAsync(t.TenancyId);
                        var depositPayments = (await _depositPaymentRepository.GetDepositPaymentsByTenancyIdAsync(t.TenancyId)).ToList();
                        foreach (var dp in depositPayments)
                        {
                            allPaymentsForRow.Add(new PaymentDetailViewModel
                            {
                                PaidOn = dp.PaidOn,
                                Amount = dp.AmountPaid,
                                Method = dp.Method ?? "",
                                Reference = dp.Reference ?? "",
                                Notes = dp.Notes ?? "",
                                CollectedBy = dp.CollectedBy ?? ""
                            });
                        }
                    }
                    // Sort by date descending for display
                    allPaymentsForRow = allPaymentsForRow.OrderByDescending(p => p.PaidOn).ToList();

                    var depositRequired = displayTenancy.DepositAmount;
                    string status;
                    if (depositPaid >= depositRequired)
                        status = "Paid";
                    else if (depositPaid > 0)
                        status = "PartPaid";
                    else
                        status = "Unpaid";

                    if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
                    {
                        string normalizedFilter = statusFilter.Replace("-", "");
                        string normalizedStatus = status.Replace("-", "");
                        if (normalizedFilter.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "Unpaid")
                            continue;
                        if (normalizedFilter.Equals("Partpaid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "PartPaid")
                            continue;
                        if (normalizedFilter.Equals("Paid", StringComparison.OrdinalIgnoreCase) && normalizedStatus != "Paid")
                            continue;
                    }

                    ledgerRows.Add(new DepositLedgerRowViewModel
                    {
                        TenancyId = displayTenancy.TenancyId,
                        HouseId = displayTenancy.HouseId,
                        HouseAddress = $"{displayTenancy.House?.AddressLine1 ?? ""}, {displayTenancy.House?.City ?? ""}".Trim(',', ' '),
                        TenantName = displayTenancy.Tenant?.FullName ?? "Unknown",
                        DepositRequired = depositRequired,
                        AmountPaid = depositPaid,
                        Status = status,
                        Payments = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(allPaymentsForRow)
                    });
                }
                catch
                {
                    // Skip this group if any error
                }
            }

            return ledgerRows.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName);
        }

        public async Task<IEnumerable<TransactionRowViewModel>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, int? houseId = null, int? tenantId = null, string? method = null)
        {
            var transactions = new List<TransactionRowViewModel>();
            
            // Get rent payments
            if (paymentType == null || paymentType == "All" || paymentType == "Rent")
            {
                var rentPayments = (await _rentPaymentRepository.GetAllRentPaymentsAsync(startDate, endDate, null))?.ToList() ?? new List<RentPayment>();
                foreach (var rp in rentPayments)
                {
                    if (rp == null) continue;
                    var tenancy = await _tenancyRepository.GetTenancyByIdAsync(rp.TenancyId);
                    if (tenancy == null) continue;
                    
                    // Apply tenant filter (check if this tenancy belongs to the selected tenant)
                    if (tenantId.HasValue && tenantId.Value != 0 && tenancy.TenantId != tenantId.Value)
                        continue;
                    
                    // Apply house filter
                    if (houseId.HasValue && tenancy.HouseId != houseId.Value)
                        continue;
                    
                    // Apply method filter - normalize method names (handle "Bank Transfer" vs "BankTransfer")
                    if (!string.IsNullOrWhiteSpace(method))
                    {
                        string normalizedMethod = method.Replace(" ", "");
                        string normalizedRpMethod = rp.Method?.Replace(" ", "") ?? "";
                        if (!string.Equals(normalizedMethod, normalizedRpMethod, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    
                    // Get period label
                    string periodLabel = "";
                    if (rp.RentChargeId.HasValue)
                    {
                        var rentCharges = (await _rentChargeRepository.GetRentChargesByTenancyIdAsync(rp.TenancyId))?.ToList() ?? new List<RentCharge>();
                        var charge = rentCharges.FirstOrDefault(rc => rc.RentChargeId == rp.RentChargeId);
                        if (charge != null)
                        {
                            periodLabel = new DateTime(charge.PeriodYear, charge.PeriodMonth, 1).ToString("MMM yyyy");
                        }
                    }
                    
                    transactions.Add(new TransactionRowViewModel
                    {
                        PaymentId = rp.RentPaymentId,
                        PaymentType = "Rent",
                        PaidOn = rp.PaidOn,
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        HouseAddress = tenancy.House != null ? $"{tenancy.House.AddressLine1}, {tenancy.House.City}" : "—",
                        PeriodLabel = periodLabel,
                        Amount = rp.AmountPaid,
                        Method = rp.Method ?? string.Empty,
                        Reference = rp.Reference ?? string.Empty,
                        Notes = rp.Notes ?? string.Empty,
                        CollectedBy = rp.CollectedBy ?? string.Empty,
                        TenancyId = rp.TenancyId,
                        RentChargeId = rp.RentChargeId
                    });
                }
            }
            
            // Get deposit payments
            if (paymentType == null || paymentType == "All" || paymentType == "Deposit")
            {
                var depositPayments = (await _depositPaymentRepository.GetAllDepositPaymentsAsync(startDate, endDate, null))?.ToList() ?? new List<DepositPayment>();
                foreach (var dp in depositPayments)
                {
                    if (dp == null) continue;
                    var tenancy = await _tenancyRepository.GetTenancyByIdAsync(dp.TenancyId);
                    if (tenancy == null) continue;
                    
                    // Apply tenant filter (check if this tenancy belongs to the selected tenant)
                    if (tenantId.HasValue && tenantId.Value != 0 && tenancy.TenantId != tenantId.Value)
                        continue;
                    
                    // Apply house filter
                    if (houseId.HasValue && tenancy.HouseId != houseId.Value)
                        continue;
                    
                    // Apply method filter - normalize method names (handle "Bank Transfer" vs "BankTransfer")
                    if (!string.IsNullOrWhiteSpace(method))
                    {
                        string normalizedMethod = method.Replace(" ", "");
                        string normalizedDpMethod = dp.Method?.Replace(" ", "") ?? "";
                        if (!string.Equals(normalizedMethod, normalizedDpMethod, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    
                    transactions.Add(new TransactionRowViewModel
                    {
                        PaymentId = dp.DepositPaymentId,
                        PaymentType = "Deposit",
                        PaidOn = dp.PaidOn,
                        TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                        HouseAddress = tenancy.House != null ? $"{tenancy.House.AddressLine1}, {tenancy.House.City}" : "—",
                        PeriodLabel = "",
                        Amount = dp.AmountPaid,
                        Method = dp.Method ?? string.Empty,
                        Reference = dp.Reference ?? string.Empty,
                        Notes = dp.Notes ?? string.Empty,
                        CollectedBy = dp.CollectedBy ?? string.Empty,
                        TenancyId = dp.TenancyId
                    });
                }
            }
            
            return transactions.OrderByDescending(t => t.PaidOn);
        }

        public async Task<IEnumerable<PaymentDetailViewModel>> GetPaymentsForRentChargeAsync(int rentChargeId)
        {
            var payments = await _rentPaymentRepository.GetRentPaymentsByChargeIdAsync(rentChargeId);
            return payments.Select(rp => new PaymentDetailViewModel
            {
                PaidOn = rp.PaidOn,
                Amount = rp.AmountPaid,
                Method = rp.Method,
                Reference = rp.Reference,
                Notes = rp.Notes,
                CollectedBy = rp.CollectedBy
            });
        }

        public async Task<decimal> GetTotalRentDueThisMonthAsync()
        {
            try
            {
                var currentDate = DateTime.Now;
                return await GetTotalUnpaidBalanceForMonthAsync(currentDate.Year, currentDate.Month).ConfigureAwait(false);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Total unpaid balance for the given month, using payments linked to each charge (RentChargeId).
        /// Deduplicates by tenancy (one balance per tenancy) so duplicate charges don't double-count.
        /// </summary>
        public async Task<decimal> GetTotalUnpaidBalanceForMonthAsync(int year, int month)
        {
            try
            {
                var charges = (await _rentChargeRepository.GetRentChargesForPeriodAsync(year, month)).ToList();
                if (charges.Count == 0) return 0m;

                // Group by tenancy (in case of duplicate charges per tenancy)
                var byTenancy = charges.GroupBy(c => c.TenancyId).ToList();
                decimal total = 0m;
                foreach (var group in byTenancy)
                {
                    decimal tenancyDue = 0m;
                    decimal tenancyPaid = 0m;
                    foreach (var charge in group)
                    {
                        tenancyDue += charge.AmountDue;
                        tenancyPaid += await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(charge.RentChargeId);
                    }
                    total += Math.Max(0, tenancyDue - tenancyPaid);
                }
                return total;
            }
            catch
            {
                return 0m;
            }
        }

        public async Task<IEnumerable<DashboardRentDueItem>> GetRentDueInNext7DaysAsync()
        {
            try
            {
                var currentDate = DateTime.Now;
                var endDate = currentDate.AddDays(7);
                var items = new List<DashboardRentDueItem>();

                // Use the ledger method which already handles all the connection logic properly
                var currentMonthRows = await GetRentLedgerForMonthAsync(currentDate.Year, currentDate.Month).ConfigureAwait(false);
                
                // Also check next month if needed
                IEnumerable<RentLedgerRowViewModel> nextMonthRows = Enumerable.Empty<RentLedgerRowViewModel>();
                if (endDate.Month != currentDate.Month || endDate.Year != currentDate.Year)
                {
                    nextMonthRows = await GetRentLedgerForMonthAsync(endDate.Year, endDate.Month).ConfigureAwait(false);
                }

                // Combine and filter for due dates in next 7 days
                var allRows = currentMonthRows.Concat(nextMonthRows)
                    .Where(r => r.DueDate >= currentDate && r.DueDate <= endDate && r.Balance > 0);

                foreach (var row in allRows)
                {
                    items.Add(new DashboardRentDueItem
                    {
                        TenancyId = row.TenancyId,
                        TenantName = row.TenantName,
                        HouseAddress = row.HouseAddress,
                        Amount = row.Balance,
                        DueDate = row.DueDate
                    });
                }

                return items.OrderBy(i => i.DueDate);
            }
            catch
            {
                return Enumerable.Empty<DashboardRentDueItem>();
            }
        }

        public async Task<IEnumerable<DashboardOverdueRentItem>> GetOverdueRentAsync()
        {
            try
            {
                var currentDate = DateTime.Now;
                var items = new List<DashboardOverdueRentItem>();

                // Use the ledger method for current month to get overdue items
                // Check current month and previous 2 months (most overdue will be in recent months)
                var monthsToCheck = new List<(int Year, int Month)>();
                for (int i = 0; i <= 2; i++)
                {
                    var checkDate = currentDate.AddMonths(-i);
                    monthsToCheck.Add((checkDate.Year, checkDate.Month));
                }

                var allRows = new List<RentLedgerRowViewModel>();
                foreach (var (year, month) in monthsToCheck)
                {
                    var rows = await GetRentLedgerForMonthAsync(year, month).ConfigureAwait(false);
                    allRows.AddRange(rows);
                }

                // Filter for overdue items (due date in past, balance > 0, status is Unpaid or Overdue)
                var overdueRows = allRows
                    .Where(r => r.DueDate < currentDate && r.Balance > 0 && (r.Status == "Unpaid" || r.Status == "Overdue"))
                    .GroupBy(r => r.TenancyId) // Group by tenancy to get only most recent overdue per tenancy
                    .Select(g => g.OrderByDescending(r => r.DueDate).First())
                    .ToList();

                foreach (var row in overdueRows)
                {
                    var daysLate = (currentDate - row.DueDate).Days;
                    items.Add(new DashboardOverdueRentItem
                    {
                        TenancyId = row.TenancyId,
                        TenantName = row.TenantName,
                        HouseAddress = row.HouseAddress,
                        Amount = row.Balance,
                        DaysLate = daysLate
                    });
                }

                return items.OrderByDescending(i => i.DaysLate);
            }
            catch
            {
                return Enumerable.Empty<DashboardOverdueRentItem>();
            }
        }

        public async Task<IEnumerable<DepositReturnReminderItem>> GetDepositReturnRemindersAsync()
        {
            try
            {
                var ended = (await _tenancyRepository.GetEndedTenanciesWithDepositAsync()).ToList();
                var tenancyIds = ended.Select(t => t.TenancyId).ToList();
                var returnedByTenancy = await _depositReturnRepository.GetByTenancyIdsAsync(tenancyIds);
                var dateFormat = Daryva.Services.DateTimeFormatProvider.DateFormat ?? "dd/MM/yyyy";
                var items = new List<DepositReturnReminderItem>();
                foreach (var t in ended)
                {
                    if (returnedByTenancy.ContainsKey(t.TenancyId)) continue; // Already recorded as returned
                    var amountPaid = await GetTotalDepositPaidAsync(t.TenancyId);
                    var usedForRent = await _rentPaymentRepository.GetTotalRentPaidFromDepositAsync(t.TenancyId);
                    var amountToReturn = amountPaid - usedForRent;
                    if (amountToReturn <= 0) continue; // Nothing to return after applying deposit-used-for-rent
                    var leaveDate = t.MoveOutDate ?? t.MoveInDate;
                    items.Add(new DepositReturnReminderItem
                    {
                        TenancyId = t.TenancyId,
                        TenantName = t.Tenant?.FullName ?? "Unknown",
                        HouseAddress = t.House != null ? $"{t.House.AddressLine1}, {t.House.City}" : "",
                        LeaveDate = leaveDate,
                        LeaveDateDisplay = leaveDate.ToString(dateFormat),
                        AmountToReturn = amountToReturn
                    });
                }
                return items.OrderByDescending(i => i.LeaveDate);
            }
            catch
            {
                return Enumerable.Empty<DepositReturnReminderItem>();
            }
        }

        public async Task RecordDepositReturnedAsync(int tenancyId, DateTime returnedDate, decimal amountReturned, string? notes = null)
        {
            var record = new Daryva.MVVM.Models.DepositReturn
            {
                TenancyId = tenancyId,
                ReturnedDate = returnedDate,
                AmountReturned = amountReturned,
                Notes = notes
            };
            await _depositReturnRepository.CreateAsync(record);
            DashboardViewModel.NotifyPaymentDataChanged();
        }

        public async Task<bool> UnrecordPaymentAsync(int paymentId, string paymentType)
        {
            try
            {
                if (paymentType == "Rent")
                {
                    // Get the payment to find the rent charge
                    var payment = await _rentPaymentRepository.GetRentPaymentByIdAsync(paymentId);
                    if (payment == null)
                    {
                        return false;
                    }

                    // Delete the payment
                    var deleted = await _rentPaymentRepository.DeleteRentPaymentAsync(paymentId);
                    if (!deleted)
                    {
                        return false;
                    }

                    // Update rent charge status if it exists
                    if (payment.RentChargeId.HasValue)
                    {
                        var rentCharge = await _rentChargeRepository.GetRentChargeByIdAsync(payment.RentChargeId.Value);
                        if (rentCharge != null)
                        {
                            // Recalculate status based on remaining payments
                            var totalPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(rentCharge.RentChargeId);
                            string newStatus;
                            if (totalPaid >= rentCharge.AmountDue)
                            {
                                newStatus = "Paid";
                            }
                            else if (totalPaid > 0)
                            {
                                // Partially paid - check if overdue
                                if (rentCharge.DueDate < DateTime.Today)
                                    newStatus = "Overdue"; // Part-paid but overdue
                                else
                                    newStatus = "PartPaid";
                            }
                            else
                            {
                                // Unpaid - check if overdue
                                if (rentCharge.DueDate < DateTime.Today)
                                    newStatus = "Overdue";
                                else
                                    newStatus = "Unpaid";
                            }
                            await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, ToDbStatus(newStatus));
                        }
                    }

                    return true;
                }
                else if (paymentType == "Deposit")
                {
                    // Delete the deposit payment
                    var deleted = await _depositPaymentRepository.DeleteDepositPaymentAsync(paymentId);
                    if (!deleted)
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
            catch
            {
                throw;
            }
            finally
            {
                // Always notify dashboard to refresh after unrecording (whether successful or not)
                DashboardViewModel.NotifyPaymentDataChanged();
            }
        }

        public async Task<bool> DeleteAllTransactionsAsync()
        {
            try
            {
                
                // Get all rent payments and delete them
                var allRentPayments = await _rentPaymentRepository.GetAllRentPaymentsAsync();
                int rentPaymentsDeleted = 0;
                foreach (var payment in allRentPayments)
                {
                    // Update rent charge status before deleting
                    if (payment.RentChargeId.HasValue)
                    {
                        var rentCharge = await _rentChargeRepository.GetRentChargeByIdAsync(payment.RentChargeId.Value);
                        if (rentCharge != null)
                        {
                            // Recalculate status based on remaining payments
                            var totalPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(rentCharge.RentChargeId);
                            string newStatus;
                            if (totalPaid >= rentCharge.AmountDue)
                            {
                                newStatus = "Paid";
                            }
                            else if (totalPaid > 0)
                            {
                                newStatus = "PartPaid";
                            }
                            else
                            {
                                newStatus = "Unpaid";
                            }
                            await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, ToDbStatus(newStatus));
                        }
                    }
                    
                    var deleted = await _rentPaymentRepository.DeleteRentPaymentAsync(payment.RentPaymentId);
                    if (deleted)
                    {
                        rentPaymentsDeleted++;
                    }
                }
                
                // Get all deposit payments and delete them
                var allDepositPayments = await _depositPaymentRepository.GetAllDepositPaymentsAsync();
                int depositPaymentsDeleted = 0;
                foreach (var payment in allDepositPayments)
                {
                    var deleted = await _depositPaymentRepository.DeleteDepositPaymentAsync(payment.DepositPaymentId);
                    if (deleted)
                    {
                        depositPaymentsDeleted++;
                    }
                }
                
                // Notify dashboard to refresh
                DashboardViewModel.NotifyPaymentDataChanged();
                
                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<int> CleanupDuplicateRentChargesAsync()
        {
            var allCharges = (await _rentChargeRepository.GetAllRentChargesAsync()).ToList();
            var groups = allCharges
                .GroupBy(c => new { c.TenancyId, c.PeriodYear, c.PeriodMonth })
                .Where(g => g.Count() > 1)
                .ToList();
            int removed = 0;
            foreach (var group in groups)
            {
                var list = group.OrderByDescending(c => c.TotalPaid).ThenBy(c => c.RentChargeId).ToList();
                var keeper = list[0];
                for (int i = 1; i < list.Count; i++)
                {
                    var duplicate = list[i];
                    await _rentPaymentRepository.ReassignPaymentsToChargeAsync(duplicate.RentChargeId, keeper.RentChargeId);
                    var deleted = await _rentChargeRepository.DeleteRentChargeAsync(duplicate.RentChargeId);
                    if (deleted) removed++;
                }
                var totalPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(keeper.RentChargeId);
                var charge = await _rentChargeRepository.GetRentChargeByIdAsync(keeper.RentChargeId);
                if (charge != null)
                {
                    string newStatus = totalPaid >= charge.AmountDue ? "Paid" : totalPaid > 0 ? "Partial" : charge.DueDate < DateTime.Today ? "Overdue" : "Pending";
                    await _rentChargeRepository.UpdateRentChargeStatusAsync(keeper.RentChargeId, newStatus);
                }
            }
            if (removed > 0)
                DashboardViewModel.NotifyPaymentDataChanged();
            return removed;
        }

        public async Task<int> RepairRentPaymentChargeLinksAsync()
        {
            var allPayments = (await _rentPaymentRepository.GetAllRentPaymentsAsync(null, null, null)).ToList();
            int updated = 0;
            foreach (var payment in allPayments)
            {
                int periodYear = payment.PaidOn.Year;
                int periodMonth = payment.PaidOn.Month;
                var charge = await _rentChargeRepository.GetRentChargeAsync(payment.TenancyId, periodYear, periodMonth);
                if (charge == null)
                {
                    var tenancy = await _tenancyRepository.GetTenancyByIdAsync(payment.TenancyId);
                    if (tenancy == null) continue;
                    var dueDate = new DateTime(periodYear, periodMonth, Math.Min((int)tenancy.PaymentDueDay, 28));
                    charge = new RentCharge
                    {
                        TenancyId = payment.TenancyId,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        AmountDue = tenancy.RentAmountMonthly,
                        DueDate = dueDate,
                        Status = "Pending"
                    };
                    charge.RentChargeId = await _rentChargeRepository.CreateRentChargeAsync(charge);
                }
                if (payment.RentChargeId != charge.RentChargeId)
                {
                    var ok = await _rentPaymentRepository.UpdateRentChargeIdAsync(payment.RentPaymentId, charge.RentChargeId);
                    if (ok) updated++;
                }
            }
            if (updated > 0)
                DashboardViewModel.NotifyPaymentDataChanged();
            return updated;
        }

        /// <summary>
        /// Maps application status to database status. SQLite RentCharge CHECK constraint allows: Pending, Paid, Overdue, Partial.
        /// </summary>
        private static string ToDbStatus(string appStatus)
        {
            return appStatus switch
            {
                "Unpaid" => "Pending",
                "PartPaid" => "Partial",
                "Paid" => "Paid",
                "Overdue" => "Overdue",
                _ => appStatus
            };
        }
    }
}
