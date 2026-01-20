using LandLordBuddy.MVVM.Models;

namespace LandLordBuddy.Services.Data
{
    public interface IExpenseRepository
    {
        Task<IEnumerable<HouseExpense>> GetAllExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null);
        Task<HouseExpense?> GetExpenseByIdAsync(int expenseId);
        Task<int> CreateExpenseAsync(HouseExpense expense);
        Task UpdateExpenseAsync(HouseExpense expense);
        Task DeleteExpenseAsync(int expenseId);
        Task<decimal> GetTotalExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null);
    }
}
