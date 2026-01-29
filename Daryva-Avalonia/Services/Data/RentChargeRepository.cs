using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class RentChargeRepository : IRentChargeRepository
    {
        private readonly IDbContext _dbContext;

        public RentChargeRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<RentCharge?> GetRentChargeAsync(int tenancyId, int periodYear, int periodMonth)
        {
            var sql = @"
                SELECT * FROM RentCharge
                WHERE TenancyId = @TenancyId
                  AND PeriodYear = @PeriodYear
                  AND PeriodMonth = @PeriodMonth";

            return await Task.FromResult(_dbContext.Query<RentCharge>(sql, new { TenancyId = tenancyId, PeriodYear = periodYear, PeriodMonth = periodMonth }).FirstOrDefault());
        }

        public async Task<RentCharge?> GetRentChargeByIdAsync(int rentChargeId)
        {
            var sql = @"
                SELECT * FROM RentCharge
                WHERE RentChargeId = @RentChargeId";

            return await Task.FromResult(_dbContext.Query<RentCharge>(sql, new { RentChargeId = rentChargeId }).FirstOrDefault());
        }

        public async Task<int> CreateRentChargeAsync(RentCharge charge)
        {
            var sql = @"
                INSERT INTO RentCharge (TenancyId, PeriodYear, PeriodMonth, AmountDue, DueDate, Status, CreatedAt)
                VALUES (@TenancyId, @PeriodYear, @PeriodMonth, @AmountDue, @DueDate, @Status, datetime('now'));
                SELECT last_insert_rowid();";

            var chargeId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, charge));
            return chargeId;
        }

        public async Task UpdateRentChargeStatusAsync(int rentChargeId, string status)
        {
            var sql = @"
                UPDATE RentCharge
                SET Status = @Status
                WHERE RentChargeId = @RentChargeId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { RentChargeId = rentChargeId, Status = status }));
        }

        public async Task<IEnumerable<RentCharge>> GetRentChargesByTenancyIdAsync(int tenancyId)
        {
            var sql = @"
                SELECT rc.*,
                       (SELECT COALESCE(SUM(rp.AmountPaid), 0)
                        FROM RentPayment rp
                        WHERE rp.RentChargeId = rc.RentChargeId) AS TotalPaid
                FROM RentCharge rc
                WHERE rc.TenancyId = @TenancyId
                ORDER BY rc.PeriodYear DESC, rc.PeriodMonth DESC";

            // Query already materializes via DbContext.Query
            var results = _dbContext.Query<RentCharge>(sql, new { TenancyId = tenancyId });
            return await Task.FromResult(results);
        }

        public async Task DeleteRentChargesByTenancyIdAsync(int tenancyId)
        {
            var sql = "DELETE FROM RentCharge WHERE TenancyId = @TenancyId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenancyId = tenancyId }));
        }
    }
}
