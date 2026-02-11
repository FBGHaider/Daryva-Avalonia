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
                INSERT INTO RentPayment (TenancyId, RentChargeId, PaidOn, AmountPaid, Method, Reference, Notes, CollectedBy, PaidFromDeposit)
                VALUES (@TenancyId, @RentChargeId, @PaidOn, @AmountPaid, @Method, @Reference, @Notes, @CollectedBy, @PaidFromDeposit);
                SELECT last_insert_rowid();";

            var paymentId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, payment));
            return paymentId;
        }

        public async Task<decimal> GetTotalRentPaidFromDepositAsync(int tenancyId)
        {
            try
            {
                var sql = @"
                    SELECT CAST(COALESCE(SUM(AmountPaid), 0) AS REAL)
                    FROM RentPayment
                    WHERE TenancyId = @TenancyId AND PaidFromDeposit = 1";
                var result = _dbContext.ExecuteScalar<object>(sql, new { TenancyId = tenancyId });
                return ConvertToDecimal(result);
            }
            catch
            {
                return 0m;
            }

            static decimal ConvertToDecimal(object? value)
            {
                if (value == null || value == DBNull.Value) return 0m;
                if (value is decimal d) return d;
                if (value is long l) return (decimal)l;
                if (value is int i) return (decimal)i;
                if (value is double db) return (decimal)db;
                if (value is string s && decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                return 0m;
            }
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

            // When no filters applied (e.g. "All" months), pass null to avoid issues with empty DynamicParameters
            var hasParams = startDate.HasValue || endDate.HasValue || tenancyId.HasValue;
            var results = _dbContext.Query<RentPayment>(sqlBuilder.ToString(), hasParams ? parameters : null);
            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<RentPayment>> GetRentPaymentsInPeriodAsync(int year, int month)
        {
            // strftime works for any PaidOn format SQLite stores (ISO, text, etc.); fixes ledger showing Overdue for left tenants
            var sql = @"
                SELECT * FROM RentPayment
                WHERE strftime('%Y', PaidOn) = @Year AND strftime('%m', PaidOn) = @Month
                ORDER BY PaidOn DESC";
            var monthStr = month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
            var results = _dbContext.Query<RentPayment>(sql, new { Year = year.ToString(), Month = monthStr });
            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<RentPayment>> GetRentPaymentsForTenancyInPeriodAsync(int tenancyId, int year, int month)
        {
            var periodStart = new DateTime(year, month, 1);
            var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var periodStartStr = periodStart.ToString("yyyy-MM-dd");
            var periodEndStr = periodEnd.ToString("yyyy-MM-dd");
            // Use date strings so SQLite comparison works reliably for all tenancies (including removed/archived)
            var sql = @"
                SELECT * FROM RentPayment
                WHERE TenancyId = @TenancyId
                  AND date(PaidOn) >= @PeriodStart
                  AND date(PaidOn) <= @PeriodEnd
                ORDER BY PaidOn DESC";
            var results = _dbContext.Query<RentPayment>(sql, new { TenancyId = tenancyId, PeriodStart = periodStartStr, PeriodEnd = periodEndStr });
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

        public async Task<bool> UpdateRentChargeIdAsync(int rentPaymentId, int rentChargeId)
        {
            var sql = @"
                UPDATE RentPayment
                SET RentChargeId = @RentChargeId
                WHERE RentPaymentId = @RentPaymentId";
            var rows = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { RentPaymentId = rentPaymentId, RentChargeId = rentChargeId }));
            return rows > 0;
        }
    }
}
