using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Data;

namespace LandLordBuddy.Services.Business
{
    public class ExpenseService : IExpenseService
    {
        private readonly IExpenseRepository _expenseRepository;

        public ExpenseService(IExpenseRepository expenseRepository)
        {
            _expenseRepository = expenseRepository;
        }

        public async Task<IEnumerable<HouseExpense>> GetExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null)
        {
            return await _expenseRepository.GetAllExpensesAsync(houseId, startDate, endDate, category, searchTerm);
        }

        public async Task<HouseExpense?> GetExpenseByIdAsync(int expenseId)
        {
            return await _expenseRepository.GetExpenseByIdAsync(expenseId);
        }

        public async Task<HouseExpense> CreateExpenseAsync(HouseExpense expense)
        {
            var expenseId = await _expenseRepository.CreateExpenseAsync(expense);
            expense.HouseExpenseId = expenseId;
            return expense;
        }

        public async Task UpdateExpenseAsync(HouseExpense expense)
        {
            await _expenseRepository.UpdateExpenseAsync(expense);
        }

        public async Task DeleteExpenseAsync(int expenseId)
        {
            await _expenseRepository.DeleteExpenseAsync(expenseId);
        }

        public async Task<ExpenseSummary> GetExpenseSummaryAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var expenses = (await _expenseRepository.GetAllExpensesAsync(houseId, startDate, endDate, null, null)).ToList();
            
            var summary = new ExpenseSummary
            {
                TotalAmount = expenses.Sum(e => e.Amount),
                TotalCount = expenses.Count,
                HighestSingleExpense = expenses.Any() ? expenses.Max(e => e.Amount) : 0
            };

            // Calculate average monthly amount
            if (startDate.HasValue && endDate.HasValue)
            {
                var months = Math.Max(1, (endDate.Value.Year - startDate.Value.Year) * 12 + (endDate.Value.Month - startDate.Value.Month) + 1);
                summary.AverageMonthlyAmount = summary.TotalAmount / months;
            }
            else if (expenses.Any())
            {
                var firstDate = expenses.Min(e => e.DateIncurred);
                var lastDate = expenses.Max(e => e.DateIncurred);
                var months = Math.Max(1, (lastDate.Year - firstDate.Year) * 12 + (lastDate.Month - firstDate.Month) + 1);
                summary.AverageMonthlyAmount = summary.TotalAmount / months;
            }

            // Biggest category
            var categoryTotals = expenses
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();
            
            if (categoryTotals != null)
            {
                summary.BiggestCategory = categoryTotals.Category;
                summary.BiggestCategoryAmount = categoryTotals.Total;
            }

            // By House
            var byHouse = expenses
                .GroupBy(e => new { e.HouseId, e.House })
                .Select(g => new ExpenseByHouse
                {
                    HouseId = g.Key.HouseId,
                    HouseAddress = g.Key.House != null ? $"{g.Key.House.AddressLine1}, {g.Key.House.City}" : "Unknown",
                    Total = g.Sum(e => e.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var totalForPercentage = summary.TotalAmount > 0 ? summary.TotalAmount : 1;
            foreach (var item in byHouse)
            {
                item.PercentageOfTotal = (item.Total / totalForPercentage) * 100;
            }
            summary.ByHouse = byHouse;

            // By Category
            var byCategory = expenses
                .GroupBy(e => e.Category)
                .Select(g => new ExpenseByCategory
                {
                    Category = g.Key,
                    Total = g.Sum(e => e.Amount),
                    Count = g.Count(),
                    Average = g.Average(e => e.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();
            summary.ByCategory = byCategory;

            // By Month
            var byMonth = expenses
                .GroupBy(e => new { e.DateIncurred.Year, e.DateIncurred.Month })
                .Select(g => new ExpenseByMonth
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy"),
                    Total = g.Sum(e => e.Amount),
                    Repairs = g.Where(e => e.Category == "Repairs").Sum(e => e.Amount),
                    Bills = g.Where(e => e.Category == "Bills" || e.Category == "CouncilTax" || e.Category == "Internet").Sum(e => e.Amount),
                    Other = g.Where(e => e.Category != "Repairs" && e.Category != "Bills" && e.Category != "CouncilTax" && e.Category != "Internet").Sum(e => e.Amount)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();
            summary.ByMonth = byMonth;

            return summary;
        }

        public async Task<string> ExportExpensesToCsvAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null)
        {
            var expenses = await _expenseRepository.GetAllExpensesAsync(houseId, startDate, endDate, category, null);
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Date,House,Category,Vendor,Description,Amount");
            
            foreach (var expense in expenses)
            {
                var houseAddress = expense.House != null ? $"{expense.House.AddressLine1}, {expense.House.City}" : "Unknown";
                var description = expense.Notes ?? "";
                var vendor = expense.Vendor ?? "";
                
                csv.AppendLine($"{expense.DateIncurred:yyyy-MM-dd},\"{houseAddress}\",{expense.Category},\"{vendor}\",\"{description}\",{expense.Amount:F2}");
            }
            
            return await Task.FromResult(csv.ToString());
        }
    }
}
