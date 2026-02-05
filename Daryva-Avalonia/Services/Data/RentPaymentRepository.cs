using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class RentPaymentRepository : IRentPaymentRepository
    {
        private readonly IDbContext _dbContext;

        public RentPaymentRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<RentPayment>> GetRentPaymentsByChargeIdAsync(int rentChargeId)
        {
            var sql = @"
                SELECT * FROM RentPayment
                WHERE RentChargeId = @RentChargeId
                ORDER BY PaidOn DESC";

            // Query already materializes via DbContext.Query, but ensure it's a list
            var results = _dbContext.Query<RentPayment>(sql, new { RentChargeId = rentChargeId });
            return await Task.FromResult(results);
        }

        public Task<decimal> GetTotalRentPaidForChargeAsync(int rentChargeId)
        {
            try
            {
                var sql = @"
                    SELECT CAST(COALESCE(SUM(AmountPaid), 0) AS REAL)
                    FROM RentPayment
                    WHERE RentChargeId = @RentChargeId";

                var result = _dbContext.ExecuteScalar<object>(sql, new { RentChargeId = rentChargeId });
                return Task.FromResult(ConvertToDecimal(result));
            }
            catch
            {
                return Task.FromResult(0m);
            }

            static decimal ConvertToDecimal(object? value)
            {
                if (value == null || value == DBNull.Value) return 0m;
                if (value is decimal d) return d;
                if (value is long l) return (decimal)l;
                if (value is int i) return (decimal)i;
                if (value is double db) return (decimal)db;
                if (value is float f) return (decimal)f;
                if (value is string s && decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                return 0m;
            }
        }

        public async Task<int> CreateRentPaymentAsync(RentPayment payment)
        {
            var sql = @"
                INSERT INTO RentPayment (TenancyId, RentChargeId, PaidOn, AmountPaid, Method, Reference, Notes, CollectedBy)
                VALUES (@TenancyId, @RentChargeId, @PaidOn, @AmountPaid, @Method, @Reference, @Notes, @CollectedBy);
                SELECT last_insert_rowid();";

            var paymentId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, payment));
            return paymentId;
        }

        public async Task<IEnumerable<RentPayment>> GetAllRentPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, int? tenancyId = null)
        {
            var sql = @"
                SELECT rp.*
                FROM RentPayment rp
                WHERE 1=1";

            var parameters = new DynamicParameters();
            var sqlBuilder = new System.Text.StringBuilder(sql);
            
            if (startDate.HasValue)
            {
                sqlBuilder.Append(" AND rp.PaidOn >= @StartDate");
                parameters.Add("StartDate", startDate.Value);
            }
            
            if (endDate.HasValue)
            {
                sqlBuilder.Append(" AND rp.PaidOn <= @EndDate");
                parameters.Add("EndDate", endDate.Value);
            }
            
            if (tenancyId.HasValue)
            {
                sqlBuilder.Append(" AND rp.TenancyId = @TenancyId");
                parameters.Add("TenancyId", tenancyId.Value);
            }
            
            sqlBuilder.Append(" ORDER BY rp.PaidOn DESC");

            // Query already materializes via DbContext.Query, but ensure it's a list
            var results = _dbContext.Query<RentPayment>(sqlBuilder.ToString(), parameters);
            return await Task.FromResult(results);
        }

        public async Task<RentPayment?> GetRentPaymentByIdAsync(int rentPaymentId)
        {
            var sql = @"
                SELECT * FROM RentPayment
                WHERE RentPaymentId = @RentPaymentId";

            var result = _dbContext.Query<RentPayment>(sql, new { RentPaymentId = rentPaymentId }).FirstOrDefault();
            return await Task.FromResult(result);
        }

        public async Task<bool> DeleteRentPaymentAsync(int rentPaymentId)
        {
            var sql = @"
                DELETE FROM RentPayment
                WHERE RentPaymentId = @RentPaymentId";

            var rowsAffected = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { RentPaymentId = rentPaymentId }));
            return rowsAffected > 0;
        }

        public async Task DeleteRentPaymentsByTenancyIdAsync(int tenancyId)
        {
            var sql = "DELETE FROM RentPayment WHERE TenancyId = @TenancyId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenancyId = tenancyId }));
        }

        public async Task<int> ReassignPaymentsToChargeAsync(int fromRentChargeId, int toRentChargeId)
        {
            var sql = @"
                UPDATE RentPayment
                SET RentChargeId = @ToRentChargeId
                WHERE RentChargeId = @FromRentChargeId";
            var rows = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { FromRentChargeId = fromRentChargeId, ToRentChargeId = toRentChargeId }));
            return rows;
        }
    }
}
