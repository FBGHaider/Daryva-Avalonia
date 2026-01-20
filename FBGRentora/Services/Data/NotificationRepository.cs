using Dapper;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Data
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly IDbContext _dbContext;

        public NotificationRepository(IDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Notification>> GetAllNotificationsAsync(string? status = null, DateTime? startDate = null, DateTime? endDate = null, string? channel = null, string? type = null, int? tenantId = null, int? houseId = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(status))
            {
                conditions.Add("n.Status = @Status");
                parameters.Add("@Status", status);
            }

            if (startDate.HasValue)
            {
                conditions.Add("n.ScheduledFor >= @StartDate");
                parameters.Add("@StartDate", startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("n.ScheduledFor <= @EndDate");
                parameters.Add("@EndDate", endDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(channel))
            {
                conditions.Add("n.Channel = @Channel");
                parameters.Add("@Channel", channel);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("n.Type = @Type");
                parameters.Add("@Type", type);
            }

            if (tenantId.HasValue)
            {
                conditions.Add("n.TenantId = @TenantId");
                parameters.Add("@TenantId", tenantId.Value);
            }

            if (houseId.HasValue)
            {
                conditions.Add("t.HouseId = @HouseId");
                parameters.Add("@HouseId", houseId.Value);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var sql = $@"
                SELECT n.*, tn.*, t.*, h.*
                FROM Notification n
                INNER JOIN Tenant tn ON n.TenantId = tn.TenantId
                LEFT JOIN Tenancy t ON n.TenancyId = t.TenancyId
                LEFT JOIN House h ON t.HouseId = h.HouseId
                {whereClause}
                ORDER BY n.ScheduledFor DESC, n.NotificationId DESC";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Notification, Tenant, Tenancy, House, Notification>(sql,
                (notification, tenant, tenancy, house) =>
                {
                    notification.Tenant = tenant;
                    notification.Tenancy = tenancy;
                    if (tenancy != null && house != null)
                    {
                        tenancy.House = house;
                    }
                    return notification;
                },
                parameters,
                splitOn: "TenantId,TenancyId,HouseId").ToList();

            return await Task.FromResult(results);
        }

        public async Task<IEnumerable<Notification>> GetPendingNotificationsAsync(DateTime? beforeDate = null)
        {
            var sql = @"
                SELECT n.*, tn.*, t.*, h.*
                FROM Notification n
                INNER JOIN Tenant tn ON n.TenantId = tn.TenantId
                LEFT JOIN Tenancy t ON n.TenancyId = t.TenancyId
                LEFT JOIN House h ON t.HouseId = h.HouseId
                WHERE n.Status = 'Pending'";

            var parameters = new DynamicParameters();
            if (beforeDate.HasValue)
            {
                sql += " AND n.ScheduledFor <= @BeforeDate";
                parameters.Add("@BeforeDate", beforeDate.Value);
            }

            sql += " ORDER BY n.ScheduledFor ASC";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Notification, Tenant, Tenancy, House, Notification>(sql,
                (notification, tenant, tenancy, house) =>
                {
                    notification.Tenant = tenant;
                    notification.Tenancy = tenancy;
                    if (tenancy != null && house != null)
                    {
                        tenancy.House = house;
                    }
                    return notification;
                },
                parameters,
                splitOn: "TenantId,TenancyId,HouseId").ToList();

            return await Task.FromResult(results);
        }

        public async Task<Notification?> GetNotificationByIdAsync(int notificationId)
        {
            var sql = @"
                SELECT n.*, tn.*, t.*, h.*
                FROM Notification n
                INNER JOIN Tenant tn ON n.TenantId = tn.TenantId
                LEFT JOIN Tenancy t ON n.TenancyId = t.TenancyId
                LEFT JOIN House h ON t.HouseId = h.HouseId
                WHERE n.NotificationId = @NotificationId";

            _dbContext.OpenConnection();
            var results = _dbContext.Connection.Query<Notification, Tenant, Tenancy, House, Notification>(sql,
                (notification, tenant, tenancy, house) =>
                {
                    notification.Tenant = tenant;
                    notification.Tenancy = tenancy;
                    if (tenancy != null && house != null)
                    {
                        tenancy.House = house;
                    }
                    return notification;
                },
                new { NotificationId = notificationId },
                splitOn: "TenantId,TenancyId,HouseId").ToList();

            return await Task.FromResult(results.FirstOrDefault());
        }

        public async Task<int> CreateNotificationAsync(Notification notification)
        {
            var sql = @"
                INSERT INTO Notification (TenantId, TenancyId, Channel, Type, ToAddress, Subject, Body, ScheduledFor, Status, TemplateId)
                VALUES (@TenantId, @TenancyId, @Channel, @Type, @ToAddress, @Subject, @Body, @ScheduledFor, @Status, @TemplateId);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var notificationId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, notification));
            return notificationId;
        }

        public async Task UpdateNotificationAsync(Notification notification)
        {
            var sql = @"
                UPDATE Notification 
                SET TenantId = @TenantId,
                    TenancyId = @TenancyId,
                    Channel = @Channel,
                    Type = @Type,
                    ToAddress = @ToAddress,
                    Subject = @Subject,
                    Body = @Body,
                    ScheduledFor = @ScheduledFor,
                    SentAt = @SentAt,
                    Status = @Status,
                    ProviderMessageId = @ProviderMessageId,
                    Error = @Error,
                    TemplateId = @TemplateId
                WHERE NotificationId = @NotificationId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, notification));
        }

        public async Task DeleteNotificationAsync(int notificationId)
        {
            var sql = "DELETE FROM Notification WHERE NotificationId = @NotificationId";
            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, new { NotificationId = notificationId }));
        }

        public async Task<IEnumerable<NotificationTemplate>> GetAllTemplatesAsync(string? channel = null, string? type = null)
        {
            var conditions = new List<string>();
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(channel))
            {
                conditions.Add("Channel = @Channel");
                parameters.Add("@Channel", channel);
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                conditions.Add("Type = @Type");
                parameters.Add("@Type", type);
            }

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var sql = $@"
                SELECT *
                FROM NotificationTemplate
                {whereClause}
                ORDER BY IsDefault DESC, Name ASC";

            return await Task.FromResult(_dbContext.Query<NotificationTemplate>(sql, parameters));
        }

        public async Task<NotificationTemplate?> GetTemplateByIdAsync(int templateId)
        {
            var sql = "SELECT * FROM NotificationTemplate WHERE TemplateId = @TemplateId";
            return await Task.FromResult(_dbContext.Query<NotificationTemplate>(sql, new { TemplateId = templateId }).FirstOrDefault());
        }

        public async Task<int> CreateTemplateAsync(NotificationTemplate template)
        {
            var sql = @"
                INSERT INTO NotificationTemplate (Name, Channel, Type, SubjectTemplate, BodyTemplate, IsDefault, CreatedAt)
                VALUES (@Name, @Channel, @Type, @SubjectTemplate, @BodyTemplate, @IsDefault, @CreatedAt);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var templateId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, template));
            return templateId;
        }

        public async Task UpdateTemplateAsync(NotificationTemplate template)
        {
            var sql = @"
                UPDATE NotificationTemplate 
                SET Name = @Name,
                    Channel = @Channel,
                    Type = @Type,
                    SubjectTemplate = @SubjectTemplate,
                    BodyTemplate = @BodyTemplate,
                    IsDefault = @IsDefault
                WHERE TemplateId = @TemplateId";

            await Task.FromResult(_dbContext.ExecuteNonQuery(sql, template));
        }

        public async Task<int> CreateAttemptAsync(NotificationAttempt attempt)
        {
            var sql = @"
                INSERT INTO NotificationAttempt (NotificationId, AttemptedAt, Status, Error, ProviderMessageId)
                VALUES (@NotificationId, @AttemptedAt, @Status, @Error, @ProviderMessageId);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            var attemptId = await Task.FromResult(_dbContext.ExecuteScalar<int>(sql, attempt));
            return attemptId;
        }

        public async Task<IEnumerable<NotificationAttempt>> GetAttemptsByNotificationIdAsync(int notificationId)
        {
            var sql = @"
                SELECT *
                FROM NotificationAttempt
                WHERE NotificationId = @NotificationId
                ORDER BY AttemptedAt DESC";

            return await Task.FromResult(_dbContext.Query<NotificationAttempt>(sql, new { NotificationId = notificationId }));
        }
    }
}
