using System;
using System.Collections.Generic;

namespace Daryva.MVVM.Models
{
    public class DateRange
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class TenantReportRow
    {
        public string FullName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Active/Ended
        public DateTime MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public string? University { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal MonthlyRent { get; set; }
        public decimal DepositRequired { get; set; }
        public decimal RentPaidToDate { get; set; }
        public decimal DepositPaidToDate { get; set; }
        public decimal DepositRemaining { get; set; }
        public string? CurrentMonthRentStatus { get; set; } // Paid/PartPaid/Unpaid
        public byte PaymentDueDay { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public bool HasTenancyAgreement { get; set; }
        public bool HasStudentLetter { get; set; }
        public DateTime? StudentLetterExpiry { get; set; }
    }

    public class ExpenseReportRow
    {
        public DateTime DateIncurred { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Vendor { get; set; }
        public string? Notes { get; set; }
        public decimal Amount { get; set; }
        public bool HasReceipt { get; set; }
    }

    public class MonthlyLedgerRow
    {
        public string TenantName { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }
        public string? RentCollectedBy { get; set; }
        public decimal DepositAmount { get; set; }
        public string? DepositCollectedBy { get; set; }
    }

    public class HouseReportExportModel
    {
        public int HouseId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public string? Postcode { get; set; }
        public int TotalRooms { get; set; }
        public DateTime GeneratedDate { get; set; }
        public DateRange? ReportRange { get; set; }
        public string OutputPath { get; set; } = string.Empty;

        // Summary totals
        public decimal TotalRentCollected { get; set; }
        public decimal TotalDepositCollected { get; set; }
        public int ActiveTenantsCount { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal OccupancyRate { get; set; }
        public DateTime? EarliestMoveInDate { get; set; }
        public DateTime? LatestPaymentDate { get; set; }

        // Data collections
        public IList<TenantReportRow> Tenants { get; set; } = new List<TenantReportRow>();
        public IList<ExpenseReportRow> Expenses { get; set; } = new List<ExpenseReportRow>();
        public IList<MonthlyLedgerRow> MonthlyLedger { get; set; } = new List<MonthlyLedgerRow>();
        public Dictionary<string, decimal> ExpensesByCategory { get; set; } = new();
        public Dictionary<string, decimal> RentCollectedByCollector { get; set; } = new();
        public IList<string> Collectors { get; set; } = new List<string>();
        public decimal RentGivenToLandlord { get; set; }
        public string? MonthYearDisplay { get; set; }
    }
}
