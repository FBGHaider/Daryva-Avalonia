using System.Linq;
using Dapper;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Data
{
    public class DepositPaymentRepository : IDepositPaymentRepository
    {
        private readonly IDbContext _dbContext;

        public DepositPaymentRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<DepositPayment>> GetDepositPaymentsByTenancyIdAsync(int tenancyId)
        {
            try
            {
                var sql = @"
                    SELECT * FROM DepositPayment
                    WHERE TenancyId = @TenancyId
                    ORDER BY PaidOn DESC";

                return await Task.FromResult(_dbContext.Query<DepositPayment>(sql, new { TenancyId = tenancyId }));
            }
            catch (Exception ex)
            {
                // If table doesn't exist yet (migration not run), return empty list
                System.Diagnostics.Debug.WriteLine($"Error getting deposit payments (table may not exist): {ex.Message}");
                return Enumerable.Empty<DepositPayment>();
            }
        }

        public async Task<decimal> GetTotalDepositPaidAsync(int tenancyId)
        {
            try
            {
                var sql = @"
                    SELECT ISNULL(SUM(AmountPaid), 0)
                    FROM DepositPayment
                    WHERE TenancyId = @TenancyId";

                return await Task.FromResult(_dbContext.ExecuteScalar<decimal>(sql, new { TenancyId = tenancyId }));
            }
            catch (Exception ex)
            {
                // If table doesn't exist yet (migration not run), return 0
                System.Diagnostics.Debug.WriteLine($"Error getting total deposit paid (table may not exist): {ex.Message}");
                return 0;
            }
        }

        public async Task<int> CreateDepositPaymentAsync(DepositPayment payment)
        {
            var sql = @"
                INSERT INTO DepositPayment (TenancyId, PaidOn, AmountPaid, Method, Reference, Notes)
                VALUES (@TenancyId, @PaidOn, @AmountPaid, @Method, @Reference, @Notes);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var paymentId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, payment));
            return paymentId;
        }

        public async Task<IEnumerable<DepositPayment>> GetAllDepositPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, int? tenancyId = null)
        {
            var sql = @"
                SELECT dp.*
                FROM DepositPayment dp
                WHERE 1=1";

            var parameters = new DynamicParameters();
            var sqlBuilder = new System.Text.StringBuilder(sql);
            
            if (startDate.HasValue)
            {
                sqlBuilder.Append(" AND dp.PaidOn >= @StartDate");
                parameters.Add("StartDate", startDate.Value);
            }
            
            if (endDate.HasValue)
            {
                sqlBuilder.Append(" AND dp.PaidOn <= @EndDate");
                parameters.Add("EndDate", endDate.Value);
            }
            
            if (tenancyId.HasValue)
            {
                sqlBuilder.Append(" AND dp.TenancyId = @TenancyId");
                parameters.Add("TenancyId", tenancyId.Value);
            }
            
            sqlBuilder.Append(" ORDER BY dp.PaidOn DESC");

            // Query already materializes via DbContext.Query, but ensure it's a list
            var results = _dbContext.Query<DepositPayment>(sqlBuilder.ToString(), parameters);
            return await Task.FromResult(results);
        }

        public async Task<DepositPayment?> GetDepositPaymentByIdAsync(int depositPaymentId)
        {
            try
            {
                var sql = @"
                    SELECT * FROM DepositPayment
                    WHERE DepositPaymentId = @DepositPaymentId";

                var result = _dbContext.Query<DepositPayment>(sql, new { DepositPaymentId = depositPaymentId }).FirstOrDefault();
                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting deposit payment by ID (table may not exist): {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteDepositPaymentAsync(int depositPaymentId)
        {
            try
            {
                var sql = @"
                    DELETE FROM DepositPayment
                    WHERE DepositPaymentId = @DepositPaymentId";

                var rowsAffected = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { DepositPaymentId = depositPaymentId }));
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting deposit payment (table may not exist): {ex.Message}");
                return false;
            }
        }
    }
}
