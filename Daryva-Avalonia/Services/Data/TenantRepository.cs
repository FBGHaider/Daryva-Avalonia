using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    public class TenantRepository : ITenantRepository
    {
        private readonly IDbContext _dbContext;

        public TenantRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT h.AddressLine1 || ', ' || h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentHouseAddress,
                       (SELECT tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentTenancyId,
                       (SELECT tn.MoveOutDate 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Ended' 
                        ORDER BY tn.MoveOutDate DESC LIMIT 1) AS LeaveDate
                FROM Tenant t
                WHERE (@IncludeArchived = 1 OR t.IsArchived = 0)
                ORDER BY t.FullName";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { IncludeArchived = includeArchived ? 1 : 0 }));
        }

        public async Task<IEnumerable<Tenant>> GetTenantsByHouseIdAsync(int? houseId, bool includeArchived = false)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT h.AddressLine1 || ', ' || h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentHouseAddress,
                       (SELECT tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentTenancyId,
                       (SELECT tn.MoveOutDate 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Ended' 
                        ORDER BY tn.MoveOutDate DESC LIMIT 1) AS LeaveDate
                FROM Tenant t
                WHERE (@IncludeArchived = 1 OR t.IsArchived = 0)
                  AND (@HouseId IS NULL OR t.TenantId IN (SELECT TenantId FROM Tenancy WHERE HouseId = @HouseId))
                ORDER BY t.FullName";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { IncludeArchived = includeArchived ? 1 : 0, HouseId = houseId }));
        }

        public async Task<Tenant?> GetTenantByIdAsync(int tenantId)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT h.AddressLine1 || ', ' || h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentHouseAddress,
                       (SELECT tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentTenancyId
                FROM Tenant t
                WHERE t.TenantId = @TenantId";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { TenantId = tenantId }).FirstOrDefault());
        }

        public async Task<int> CreateTenantAsync(Tenant tenant)
        {
            var sql = @"
                INSERT INTO Tenant (FullName, PhoneNumber, Email, UniversityName, CreatedAt, IsArchived)
                VALUES (@FullName, @PhoneNumber, @Email, @UniversityName, datetime('now'), 0);
                SELECT last_insert_rowid();";

            var tenantId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, tenant));
            return tenantId;
        }

        public async Task UpdateTenantAsync(Tenant tenant)
        {
            var sql = @"
                UPDATE Tenant 
                SET FullName = @FullName,
                    PhoneNumber = @PhoneNumber,
                    Email = @Email,
                    UniversityName = @UniversityName,
                    IsArchived = @IsArchived
                WHERE TenantId = @TenantId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, tenant));
        }

        public async Task ArchiveTenantAsync(int tenantId)
        {
            var sql = "UPDATE Tenant SET IsArchived = 1 WHERE TenantId = @TenantId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenantId = tenantId }));
        }

        public async Task UnarchiveTenantAsync(int tenantId)
        {
            var sql = "UPDATE Tenant SET IsArchived = 0 WHERE TenantId = @TenantId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenantId = tenantId }));
        }

        public async Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT h.AddressLine1 || ', ' || h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentHouseAddress,
                       (SELECT tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active'
                        LIMIT 1) AS CurrentTenancyId
                FROM Tenant t
                WHERE t.IsArchived = 0
                  AND (t.FullName LIKE @SearchTerm 
                   OR t.Email LIKE @SearchTerm 
                   OR t.PhoneNumber LIKE @SearchTerm
                   OR t.UniversityName LIKE @SearchTerm)
                ORDER BY t.FullName";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { SearchTerm = $"%{searchTerm}%" }));
        }

        public async Task DeleteTenantAsync(int tenantId)
        {
            // Delete tenant - this will cascade delete related records if foreign keys are set up that way
            // Otherwise, we need to delete tenancies first
            var sql = "DELETE FROM Tenant WHERE TenantId = @TenantId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { TenantId = tenantId }));
        }
    }
}
