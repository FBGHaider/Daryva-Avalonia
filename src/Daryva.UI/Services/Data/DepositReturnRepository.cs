using System.Collections.Generic;
using System.Linq;
using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class DepositReturnRepository : IDepositReturnRepository
    {
        private readonly IDbContext _dbContext;

        public DepositReturnRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<int> CreateAsync(DepositReturn depositReturn)
        {
            var sql = @"
                INSERT INTO DepositReturn (TenancyId, ReturnedDate, AmountReturned, Notes)
                VALUES (@TenancyId, @ReturnedDate, @AmountReturned, @Notes);
                SELECT last_insert_rowid();";
            return await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, depositReturn));
        }

        public async Task<DepositReturn?> GetByTenancyIdAsync(int tenancyId)
        {
            try
            {
                var sql = "SELECT * FROM DepositReturn WHERE TenancyId = @TenancyId ORDER BY ReturnedDate DESC LIMIT 1";
                return (await Task.FromResult(_dbContext.Query<DepositReturn>(sql, new { TenancyId = tenancyId }))).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyDictionary<int, DepositReturn>> GetByTenancyIdsAsync(IEnumerable<int> tenancyIds)
        {
            var idSet = tenancyIds.Distinct().ToHashSet();
            if (idSet.Count == 0) return new Dictionary<int, DepositReturn>();
            try
            {
                var sql = "SELECT * FROM DepositReturn ORDER BY TenancyId, ReturnedDate DESC";
                var all = (await Task.FromResult(_dbContext.Query<DepositReturn>(sql))).ToList();
                return all.Where(r => idSet.Contains(r.TenancyId)).GroupBy(r => r.TenancyId).ToDictionary(g => g.Key, g => g.First());
            }
            catch
            {
                return new Dictionary<int, DepositReturn>();
            }
        }
    }
}
