using Dapper;
using Daryva.MVVM.Models;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    /// <summary>
    /// Repository implementation for Document operations using Dapper.
    /// </summary>
    public class DocumentRepository : IDocumentRepository
    {
        private readonly IDbContext _dbContext;

        public DocumentRepository(IDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<IEnumerable<Document>> GetDocumentsByTenantIdAsync(int tenantId)
        {
            var sql = @"
                SELECT * 
                FROM Document 
                WHERE TenantId = @TenantId
                ORDER BY UploadedAt DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, new { TenantId = tenantId }));
        }

        public async Task<IEnumerable<Document>> GetDocumentsByTenancyIdAsync(int tenancyId)
        {
            var sql = @"
                SELECT * 
                FROM Document 
                WHERE TenancyId = @TenancyId
                ORDER BY UploadedAt DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, new { TenancyId = tenancyId }));
        }

        public async Task<IEnumerable<Document>> GetDocumentsByHouseIdAsync(int houseId)
        {
            var sql = @"
                SELECT * 
                FROM Document 
                WHERE HouseId = @HouseId
                ORDER BY UploadedAt DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, new { HouseId = houseId }));
        }

        public async Task<IEnumerable<Document>> GetAllDocumentsAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null, string? type = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (tenantId.HasValue)
            {
                conditions.Add("TenantId = @TenantId");
                parameters.Add("@TenantId", tenantId.Value);
            }

            if (tenancyId.HasValue)
            {
                conditions.Add("TenancyId = @TenancyId");
                parameters.Add("@TenancyId", tenancyId.Value);
            }

            if (houseId.HasValue)
            {
                conditions.Add("HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("Type = @Type");
                parameters.Add("@Type", type);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            var sql = $@"
                SELECT * 
                FROM Document 
                {whereClause}
                ORDER BY UploadedAt DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, parameters));
        }

        public async Task<Document?> GetDocumentByIdAsync(int documentId)
        {
            var sql = @"
                SELECT * 
                FROM Document 
                WHERE DocumentId = @DocumentId";

            return await Task.FromResult(_dbContext.Query<Document>(sql, new { DocumentId = documentId }).FirstOrDefault());
        }

        public async Task<Document?> GetActiveDocumentAsync(int? tenantId, int? tenancyId, int? houseId, string type)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (tenantId.HasValue)
            {
                conditions.Add("TenantId = @TenantId");
                parameters.Add("@TenantId", tenantId.Value);
            }

            if (tenancyId.HasValue)
            {
                conditions.Add("TenancyId = @TenancyId");
                parameters.Add("@TenancyId", tenancyId.Value);
            }

            if (houseId.HasValue)
            {
                conditions.Add("HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            conditions.Add("Type = @Type");
            parameters.Add("@Type", type);

            conditions.Add("IsActive = 1");

            var whereClause = string.Join(" AND ", conditions);
            var sql = $@"
                SELECT * 
                FROM Document 
                WHERE {whereClause}
                ORDER BY Version DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, parameters).FirstOrDefault());
        }

        public async Task<int> CreateDocumentAsync(Document document)
        {
            var sql = @"
                INSERT INTO Document (TenantId, TenancyId, HouseId, Type, DisplayName, FileName, FileMimeType, StoragePath, Source, UploadedAt, ValidFrom, ValidTo, Version, IsActive)
                OUTPUT INSERTED.DocumentId
                VALUES (@TenantId, @TenancyId, @HouseId, @Type, @DisplayName, @FileName, @FileMimeType, @StoragePath, @Source, @UploadedAt, @ValidFrom, @ValidTo, @Version, @IsActive)";

            var parameters = new
            {
                document.TenantId,
                document.TenancyId,
                document.HouseId,
                document.Type,
                document.DisplayName,
                document.FileName,
                document.FileMimeType,
                document.StoragePath,
                document.Source,
                UploadedAt = document.UploadedAt,
                document.ValidFrom,
                document.ValidTo,
                document.Version,
                IsActive = document.IsActive ? 1 : 0
            };

            return await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, parameters));
        }

        public async Task<bool> UpdateDocumentAsync(Document document)
        {
            var sql = @"
                UPDATE Document 
                SET TenantId = @TenantId,
                    TenancyId = @TenancyId,
                    HouseId = @HouseId,
                    Type = @Type,
                    DisplayName = @DisplayName,
                    FileName = @FileName,
                    FileMimeType = @FileMimeType,
                    StoragePath = @StoragePath,
                    Source = @Source,
                    ValidFrom = @ValidFrom,
                    ValidTo = @ValidTo,
                    Version = @Version,
                    IsActive = @IsActive
                WHERE DocumentId = @DocumentId";

            var parameters = new
            {
                document.DocumentId,
                document.TenantId,
                document.TenancyId,
                document.HouseId,
                document.Type,
                document.DisplayName,
                document.FileName,
                document.FileMimeType,
                document.StoragePath,
                document.Source,
                document.ValidFrom,
                document.ValidTo,
                document.Version,
                IsActive = document.IsActive ? 1 : 0
            };

            var rowsAffected = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, parameters));
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteDocumentAsync(int documentId)
        {
            var sql = "DELETE FROM Document WHERE DocumentId = @DocumentId";
            var rowsAffected = await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { DocumentId = documentId }));
            return rowsAffected > 0;
        }

        public async Task<IEnumerable<Document>> GetExpiringDocumentsAsync(int daysAhead = 30)
        {
            var sql = @"
                SELECT * 
                FROM Document 
                WHERE ValidTo IS NOT NULL 
                  AND ValidTo >= CAST(GETDATE() AS DATE)
                  AND ValidTo <= CAST(DATEADD(DAY, @DaysAhead, GETDATE()) AS DATE)
                  AND IsActive = 1
                ORDER BY ValidTo ASC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, new { DaysAhead = daysAhead }));
        }

        public async Task<IEnumerable<Document>> GetDocumentHistoryAsync(int? tenantId, int? tenancyId, int? houseId, string type)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (tenantId.HasValue)
            {
                conditions.Add("TenantId = @TenantId");
                parameters.Add("@TenantId", tenantId.Value);
            }

            if (tenancyId.HasValue)
            {
                conditions.Add("TenancyId = @TenancyId");
                parameters.Add("@TenancyId", tenancyId.Value);
            }

            if (houseId.HasValue)
            {
                conditions.Add("HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            conditions.Add("Type = @Type");
            parameters.Add("@Type", type);

            var whereClause = string.Join(" AND ", conditions);
            var sql = $@"
                SELECT * 
                FROM Document 
                WHERE {whereClause}
                ORDER BY Version DESC";

            return await Task.FromResult(_dbContext.Query<Document>(sql, parameters));
        }
    }
}
