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

        public async Task<decimal> GetTotalRentPaidForChargeAsync(int rentChargeId)
        {
            var sql = @"
                SELECT ISNULL(SUM(AmountPaid), 0)
                FROM RentPayment
                WHERE RentChargeId = @RentChargeId";

            return await Task.FromResult(_dbContext.ExecuteScalar<decimal>(sql, new { RentChargeId = rentChargeId }));
        }

        public async Task<int> CreateRentPaymentAsync(RentPayment payment)
        {
            var sql = @"
                INSERT INTO RentPayment (TenancyId, RentChargeId, PaidOn, AmountPaid, Method, Reference, Notes)
                VALUES (@TenancyId, @RentChargeId, @PaidOn, @AmountPaid, @Method, @Reference, @Notes);
                SELECT CAST(SCOPE_IDENTITY() as int);";

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
    }
}
