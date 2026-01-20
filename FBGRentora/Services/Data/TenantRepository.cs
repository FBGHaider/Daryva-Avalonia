using Dapper;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Data
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
                       (SELECT TOP 1 h.AddressLine1 + ', ' + h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentHouseAddress,
                       (SELECT TOP 1 tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentTenancyId
                FROM Tenant t
                WHERE (@IncludeArchived = 1 OR t.IsArchived = 0)
                ORDER BY t.FullName";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { IncludeArchived = includeArchived ? 1 : 0 }));
        }

        public async Task<Tenant?> GetTenantByIdAsync(int tenantId)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT TOP 1 h.AddressLine1 + ', ' + h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentHouseAddress,
                       (SELECT TOP 1 tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentTenancyId
                FROM Tenant t
                WHERE t.TenantId = @TenantId";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { TenantId = tenantId }).FirstOrDefault());
        }

        public async Task<int> CreateTenantAsync(Tenant tenant)
        {
            var sql = @"
                INSERT INTO Tenant (FullName, PhoneNumber, Email, UniversityName, CreatedAt, IsArchived)
                VALUES (@FullName, @PhoneNumber, @Email, @UniversityName, GETUTCDATE(), 0);
                SELECT CAST(SCOPE_IDENTITY() as int);";

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

        public async Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm)
        {
            var sql = @"
                SELECT t.*,
                       (SELECT TOP 1 h.AddressLine1 + ', ' + h.City 
                        FROM Tenancy tn 
                        INNER JOIN House h ON tn.HouseId = h.HouseId 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentHouseAddress,
                       (SELECT TOP 1 tn.TenancyId 
                        FROM Tenancy tn 
                        WHERE tn.TenantId = t.TenantId AND tn.Status = 'Active') AS CurrentTenancyId
                FROM Tenant t
                WHERE t.IsArchived = 0
                  AND (t.FullName LIKE @SearchTerm 
                   OR t.Email LIKE @SearchTerm 
                   OR t.PhoneNumber LIKE @SearchTerm
                   OR t.UniversityName LIKE @SearchTerm)
                ORDER BY t.FullName";

            return await Task.FromResult(_dbContext.Query<Tenant>(sql, new { SearchTerm = $"%{searchTerm}%" }));
        }
    }
}
