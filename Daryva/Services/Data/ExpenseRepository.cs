using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly IDbContext _dbContext;

        public ExpenseRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<HouseExpense>> GetAllExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (houseId.HasValue)
            {
                conditions.Add("e.HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            if (startDate.HasValue)
            {
                conditions.Add("e.DateIncurred >= @StartDate");
                parameters.Add("@StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("e.DateIncurred <= @EndDate");
                parameters.Add("@EndDate", endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                conditions.Add("e.Category = @Category");
                parameters.Add("@Category", category);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                conditions.Add("(e.Vendor LIKE @SearchTerm OR e.Notes LIKE @SearchTerm)");
                parameters.Add("@SearchTerm", $"%{searchTerm}%");
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            
            var sql = $@"
                SELECT e.*, h.*
                FROM HouseExpense e
                INNER JOIN House h ON e.HouseId = h.HouseId
                {whereClause}
                ORDER BY e.DateIncurred DESC, e.HouseExpenseId DESC";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<HouseExpense, House, HouseExpense>(sql,
                (expense, house) =>
                {
                    expense.House = house;
                    return expense;
                },
                parameters,
                splitOn: "HouseId").ToList();
            
            return await Task.FromResult(results);
        }

        public async Task<HouseExpense?> GetExpenseByIdAsync(int expenseId)
        {
            var sql = @"
                SELECT e.*, h.*
                FROM HouseExpense e
                INNER JOIN House h ON e.HouseId = h.HouseId
                WHERE e.HouseExpenseId = @ExpenseId";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<HouseExpense, House, HouseExpense>(sql,
                (expense, house) =>
                {
                    expense.House = house;
                    return expense;
                },
                new { ExpenseId = expenseId },
                splitOn: "HouseId").ToList();

            return await Task.FromResult(results.FirstOrDefault());
        }

        public async Task<int> CreateExpenseAsync(HouseExpense expense)
        {
            var sql = @"
                INSERT INTO HouseExpense (HouseId, DateIncurred, Category, Amount, Vendor, Notes, ReceiptDocumentId)
                VALUES (@HouseId, @DateIncurred, @Category, @Amount, @Vendor, @Notes, @ReceiptDocumentId);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var expenseId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, expense));
            return expenseId;
        }

        public async Task UpdateExpenseAsync(HouseExpense expense)
        {
            var sql = @"
                UPDATE HouseExpense 
                SET HouseId = @HouseId,
                    DateIncurred = @DateIncurred,
                    Category = @Category,
                    Amount = @Amount,
                    Vendor = @Vendor,
                    Notes = @Notes,
                    ReceiptDocumentId = @ReceiptDocumentId
                WHERE HouseExpenseId = @HouseExpenseId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, expense));
        }

        public async Task DeleteExpenseAsync(int expenseId)
        {
            var sql = "DELETE FROM HouseExpense WHERE HouseExpenseId = @ExpenseId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { ExpenseId = expenseId }));
        }

        public async Task<decimal> GetTotalExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (houseId.HasValue)
            {
                conditions.Add("HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            if (startDate.HasValue)
            {
                conditions.Add("DateIncurred >= @StartDate");
                parameters.Add("@StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("DateIncurred <= @EndDate");
                parameters.Add("@EndDate", endDate.Value);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            
            var sql = $@"
                SELECT ISNULL(SUM(Amount), 0)
                FROM HouseExpense
                {whereClause}";

            return await Task.FromResult(_dbContext.ExecuteScalar<decimal>(sql, parameters));
        }
    }
}
