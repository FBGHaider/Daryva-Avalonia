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
        private readonly IRentChargeRepository _rentChargeRepository;
        private readonly IRentPaymentRepository _rentPaymentRepository;
        private readonly ITenancyRepository _tenancyRepository;

        public PaymentService(
            IDepositPaymentRepository depositPaymentRepository,
            IRentChargeRepository rentChargeRepository,
            IRentPaymentRepository rentPaymentRepository,
            ITenancyRepository tenancyRepository)
        {
            _depositPaymentRepository = depositPaymentRepository;
            _rentChargeRepository = rentChargeRepository;
            _rentPaymentRepository = rentPaymentRepository;
            _tenancyRepository = tenancyRepository;
        }

        public async Task RecordPaymentAsync(int tenancyId, decimal depositAmount, decimal rentAmount, int rentYear, int rentMonth, DateTime paymentDate, string method, string? reference, string? notes)
        {
            // Get tenancy to get rent amount and payment due day
            var tenancy = await _tenancyRepository.GetTenancyByIdAsync(tenancyId);
            if (tenancy == null)
                throw new InvalidOperationException($"Tenancy {tenancyId} not found");

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
                    Notes = notes
                };
                await _depositPaymentRepository.CreateDepositPaymentAsync(depositPayment);
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
                        Status = "Unpaid"
                    };
                    rentCharge.RentChargeId = await _rentChargeRepository.CreateRentChargeAsync(rentCharge);
                }

                // Create rent payment
                var rentPayment = new RentPayment
                {
                    TenancyId = tenancyId,
                    RentChargeId = rentCharge.RentChargeId,
                    PaidOn = paymentDate,
                    AmountPaid = rentAmount,
                    Method = method,
                    Reference = reference,
                    Notes = notes
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

                await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, newStatus);
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
            // Get all active tenancies and materialize the list to close the DataReader
            var tenancies = (await _tenancyRepository.GetActiveTenanciesAsync()).ToList();
            
            var ledgerRows = new List<RentLedgerRowViewModel>();
            
            foreach (var tenancy in tenancies)
            {
                // Apply house filter
                if (houseId.HasValue && tenancy.HouseId != houseId.Value)
                    continue;
                
                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var searchLower = searchTerm.ToLower();
                    if (!tenancy.Tenant?.FullName.ToLower().Contains(searchLower) == true &&
                        !tenancy.House?.AddressLine1.ToLower().Contains(searchLower) == true)
                        continue;
                }
                
                // Skip periods before the tenancy start date (MoveInDate)
                // Tenants don't owe rent for periods before they moved in
                var periodStartDate = new DateTime(year, month, 1);
                if (tenancy.MoveInDate > periodStartDate.AddMonths(1).AddDays(-1)) // If MoveInDate is after the last day of this period
                {
                    continue; // Skip this period - tenant wasn't renting yet
                }
                
                // Get or create rent charge for this month
                var rentCharge = await _rentChargeRepository.GetRentChargeAsync(tenancy.TenancyId, year, month);
                decimal amountDue = rentCharge?.AmountDue ?? tenancy.RentAmountMonthly;
                decimal amountPaid = 0;
                DateTime dueDate = rentCharge?.DueDate ?? new DateTime(year, month, Math.Min((int)tenancy.PaymentDueDay, 28));
                
                if (rentCharge != null)
                {
                    amountPaid = await _rentPaymentRepository.GetTotalRentPaidForChargeAsync(rentCharge.RentChargeId);
                }
                
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
                    // Normalize status filter values (handle both "Part-paid" and "PartPaid")
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
                
                // Get deposit remaining
                var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                var depositRemaining = Math.Max(0, tenancy.DepositAmount - depositPaid);
                
                // Get payments for this month
                var payments = new List<PaymentDetailViewModel>();
                if (rentCharge != null)
                {
                    var rentPayments = (await _rentPaymentRepository.GetRentPaymentsByChargeIdAsync(rentCharge.RentChargeId)).ToList();
                    payments.AddRange(rentPayments.Select(rp => new PaymentDetailViewModel
                    {
                        PaidOn = rp.PaidOn,
                        Amount = rp.AmountPaid,
                        Method = rp.Method,
                        Reference = rp.Reference,
                        Notes = rp.Notes
                    }));
                }
                
                ledgerRows.Add(new RentLedgerRowViewModel
                {
                    TenancyId = tenancy.TenancyId,
                    HouseAddress = $"{tenancy.House?.AddressLine1}, {tenancy.House?.City}",
                    TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                    DueDate = dueDate,
                    AmountDue = amountDue,
                    AmountPaid = amountPaid,
                    Status = status,
                    DepositRemaining = depositRemaining,
                    PaymentsForThisMonth = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                });
            }
            
            return ledgerRows.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName);
        }

        public async Task<IEnumerable<DepositLedgerRowViewModel>> GetDepositLedgerForMonthAsync(int? houseId = null, string? statusFilter = null, string? searchTerm = null)
        {
            // Get all active tenancies and materialize the list to close the DataReader
            var tenancies = (await _tenancyRepository.GetActiveTenanciesAsync()).ToList();
            
            var ledgerRows = new List<DepositLedgerRowViewModel>();
            
            foreach (var tenancy in tenancies)
            {
                // Apply house filter
                if (houseId.HasValue && tenancy.HouseId != houseId.Value)
                    continue;
                
                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var searchLower = searchTerm.ToLower();
                    if (!tenancy.Tenant?.FullName.ToLower().Contains(searchLower) == true &&
                        !tenancy.House?.AddressLine1.ToLower().Contains(searchLower) == true)
                        continue;
                }
                
                // Get deposit information
                var depositRequired = tenancy.DepositAmount;
                var depositPaid = await GetTotalDepositPaidAsync(tenancy.TenancyId);
                var balance = depositRequired - depositPaid;
                
                // Calculate status
                string status;
                if (depositPaid >= depositRequired)
                    status = "Paid";
                else if (depositPaid > 0)
                    status = "PartPaid";
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
                }
                
                // Get all deposit payments for this tenancy
                var depositPayments = (await _depositPaymentRepository.GetDepositPaymentsByTenancyIdAsync(tenancy.TenancyId)).ToList();
                var payments = depositPayments.Select(dp => new PaymentDetailViewModel
                {
                    PaidOn = dp.PaidOn,
                    Amount = dp.AmountPaid,
                    Method = dp.Method,
                    Reference = dp.Reference,
                    Notes = dp.Notes
                }).ToList();
                
                ledgerRows.Add(new DepositLedgerRowViewModel
                {
                    TenancyId = tenancy.TenancyId,
                    HouseAddress = $"{tenancy.House?.AddressLine1}, {tenancy.House?.City}",
                    TenantName = tenancy.Tenant?.FullName ?? "Unknown",
                    DepositRequired = depositRequired,
                    AmountPaid = depositPaid,
                    Status = status,
                    Payments = new System.Collections.ObjectModel.ObservableCollection<PaymentDetailViewModel>(payments)
                });
            }
            
            return ledgerRows.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName);
        }

        public async Task<IEnumerable<TransactionRowViewModel>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, int? houseId = null, int? tenantId = null, string? method = null)
        {
            var transactions = new List<TransactionRowViewModel>();
            
            // Get rent payments
            if (paymentType == null || paymentType == "All" || paymentType == "Rent")
            {
                // Get all rent payments (filtering by tenantId will be done in the loop since repository uses tenancyId)
                var rentPayments = (await _rentPaymentRepository.GetAllRentPaymentsAsync(startDate, endDate, null)).ToList();
                
                foreach (var rp in rentPayments)
                {
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
                        var rentCharges = (await _rentChargeRepository.GetRentChargesByTenancyIdAsync(rp.TenancyId)).ToList();
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
                        HouseAddress = $"{tenancy.House?.AddressLine1}, {tenancy.House?.City}",
                        PeriodLabel = periodLabel,
                        Amount = rp.AmountPaid,
                        Method = rp.Method,
                        Reference = rp.Reference,
                        Notes = rp.Notes,
                        TenancyId = rp.TenancyId,
                        RentChargeId = rp.RentChargeId
                    });
                }
            }
            
            // Get deposit payments
            if (paymentType == null || paymentType == "All" || paymentType == "Deposit")
            {
                // Get all deposit payments (filtering by tenantId will be done in the loop since repository uses tenancyId)
                var depositPayments = (await _depositPaymentRepository.GetAllDepositPaymentsAsync(startDate, endDate, null)).ToList();
                
                foreach (var dp in depositPayments)
                {
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
                        HouseAddress = $"{tenancy.House?.AddressLine1}, {tenancy.House?.City}",
                        PeriodLabel = "",
                        Amount = dp.AmountPaid,
                        Method = dp.Method,
                        Reference = dp.Reference,
                        Notes = dp.Notes,
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
                Notes = rp.Notes
            });
        }

        public async Task<decimal> GetTotalRentDueThisMonthAsync()
        {
            try
            {
                var currentDate = DateTime.Now;
                // Use the existing ledger method which already handles connection properly
                var ledgerRows = await GetRentLedgerForMonthAsync(currentDate.Year, currentDate.Month).ConfigureAwait(false);
                
                // Sum up all unpaid and part-paid amounts for this month
                return ledgerRows
                    .Where(r => r.Status == "Unpaid" || r.Status == "PartPaid")
                    .Sum(r => r.Balance);
            }
            catch
            {
                return 0;
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
                            await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, newStatus);
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
                            await _rentChargeRepository.UpdateRentChargeStatusAsync(rentCharge.RentChargeId, newStatus);
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
    }
}
