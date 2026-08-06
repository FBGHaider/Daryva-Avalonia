using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IExpenseService
    {
        Task<IEnumerable<HouseExpense>> GetExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null);
        Task<HouseExpense?> GetExpenseByIdAsync(int expenseId);
        Task<HouseExpense> CreateExpenseAsync(HouseExpense expense);
        Task UpdateExpenseAsync(HouseExpense expense);
        Task DeleteExpenseAsync(int expenseId);
        Task<ExpenseSummary> GetExpenseSummaryAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null);
        Task<string> ExportExpensesToCsvAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null);
    }

    public class ExpenseSummary
    {
        public decimal TotalAmount { get; set; }
        public decimal AverageMonthlyAmount { get; set; }
        public string BiggestCategory { get; set; } = string.Empty;
        public decimal BiggestCategoryAmount { get; set; }
        public decimal HighestSingleExpense { get; set; }
        public int TotalCount { get; set; }
        public List<ExpenseByHouse> ByHouse { get; set; } = new();
        public List<ExpenseByCategory> ByCategory { get; set; } = new();
        public List<ExpenseByMonth> ByMonth { get; set; } = new();
    }

    public class ExpenseByHouse
    {
        public int HouseId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public int Count { get; set; }
    }

    public class ExpenseByCategory
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public int Count { get; set; }
        public decimal Average { get; set; }
    }

    public class ExpenseByMonth
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Repairs { get; set; }
        public decimal Bills { get; set; }
        public decimal Other { get; set; }
    }
}
