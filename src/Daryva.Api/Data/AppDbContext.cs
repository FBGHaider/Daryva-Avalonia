using Daryva.Api.Domain;
using Daryva.Api.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Data;

/// <summary>
/// Multi-tenant EF Core DbContext for Daryva API.
/// Automatically filters all IOrgScopedEntity queries by CurrentOrgId via global query filters.
/// This ensures data isolation even if a developer forgets a WHERE clause.
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public required DbSet<Organization> Organizations { get; set; }
    public required DbSet<OrganizationMember> OrganizationMembers { get; set; }
    public required DbSet<OrganizationInvite> OrganizationInvites { get; set; }
    public required DbSet<OrganizationJoinCode> OrganizationJoinCodes { get; set; }
    public required DbSet<AppUser> AppUsers { get; set; }
    public required DbSet<AppUserProfile> AppUserProfiles { get; set; }
    public required DbSet<AuthRefreshToken> AuthRefreshTokens { get; set; }
    public required DbSet<House> Houses { get; set; }
    public required DbSet<Tenant> Tenants { get; set; }
    public required DbSet<Tenancy> Tenancies { get; set; }
    public required DbSet<Expense> Expenses { get; set; }
    public required DbSet<Document> Documents { get; set; }
    public required DbSet<RentPayment> RentPayments { get; set; }
    public required DbSet<DepositPayment> DepositPayments { get; set; }
    public required DbSet<DepositReturn> DepositReturns { get; set; }
    public required DbSet<Notification> Notifications { get; set; }
    public required DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public required DbSet<NotificationAttempt> NotificationAttempts { get; set; }
    public required DbSet<AuditLog> AuditLogs { get; set; }
    public required DbSet<SupportSession> SupportSessions { get; set; }
    public required DbSet<SupportAccessCode> SupportAccessCodes { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ========== ORGANIZATION ==========
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasMany(e => e.Members)
                .WithOne(m => m.Organization)
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Houses)
                .WithOne(h => h.Organization)
                .HasForeignKey(h => h.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Invites)
                .WithOne(i => i.Organization)
                .HasForeignKey(i => i.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.JoinCodes)
                .WithOne(c => c.Organization)
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ========== ORGANIZATION MEMBER ==========
        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique: User can only have one role per org
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
        });

        // ========== ORGANIZATION INVITE (Global Entity) ==========
        modelBuilder.Entity<OrganizationInvite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UsedByUserId).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // ========== ORGANIZATION JOIN CODE (Global Entity) ==========
        modelBuilder.Entity<OrganizationJoinCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).IsRequired().HasMaxLength(512);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.CodeHash).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== APP USER PROFILE (OIDC/Dev - Id = provider sub) ==========
        modelBuilder.Entity<AppUserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(256);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.TimeZoneId).HasMaxLength(128);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Email);
        });

        // ========== APP USER (Global/Auth Entity - local email/password) ==========
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.EmailVerificationTokenHash).HasMaxLength(512);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.EmailVerificationTokenHash).IsUnique();
        });

        // ========== REFRESH TOKEN (Global/Auth Entity) ==========
        modelBuilder.Entity<AuthRefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(512);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedByIp).HasMaxLength(128);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // ========== HOUSE (Org-Scoped Entity) ==========
        modelBuilder.Entity<House>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.AddressLine1).IsRequired().HasMaxLength(256);
            entity.Property(e => e.AddressLine2).HasMaxLength(256);
            entity.Property(e => e.City).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Postcode).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TotalRooms).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Index on OrganizationId for query filtering
            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== TENANT (Org-Scoped Entity) ==========
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.UniversityName).HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.InviteTokenHash).HasMaxLength(512);
            entity.HasIndex(e => e.OrganizationId);

            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(e => e.AppUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.AppUserId).IsUnique();
            entity.HasIndex(e => e.InviteTokenHash).IsUnique();
        });

        // ========== TENANCY (Org-Scoped Entity) ==========
        modelBuilder.Entity<Tenancy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RentAmountMonthly).HasPrecision(18, 2);
            entity.Property(e => e.DepositAmount).HasPrecision(18, 2);
            
            entity.HasOne(e => e.House)
                .WithMany()
                .HasForeignKey(e => e.HouseId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Tenancies)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.HouseId);
            entity.HasIndex(e => e.TenantId);
        });

        // ========== EXPENSE (Org-Scoped Entity) ==========
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Vendor).HasMaxLength(256);
            
            entity.HasOne(e => e.House)
                .WithMany()
                .HasForeignKey(e => e.HouseId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.HouseId);
        });

        // ========== DOCUMENT (Org-Scoped Entity) ==========
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(512);
            entity.Property(e => e.FileMimeType).HasMaxLength(128);
            entity.Property(e => e.StoragePath).HasMaxLength(1024);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Tenancy)
                .WithMany()
                .HasForeignKey(e => e.TenancyId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.House)
                .WithMany()
                .HasForeignKey(e => e.HouseId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== RENT PAYMENT (Org-Scoped Entity) ==========
        modelBuilder.Entity<RentPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.CollectedBy).HasMaxLength(256);
            
            entity.HasOne(e => e.Tenancy)
                .WithMany()
                .HasForeignKey(e => e.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.TenancyId);
        });

        // ========== DEPOSIT PAYMENT (Org-Scoped Entity) ==========
        modelBuilder.Entity<DepositPayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.AmountPaid).HasPrecision(18, 2);
            entity.Property(e => e.PaymentMethod).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProtectionScheme).HasMaxLength(256);
            entity.Property(e => e.ProtectionReference).HasMaxLength(256);
            
            entity.HasOne(e => e.Tenancy)
                .WithMany()
                .HasForeignKey(e => e.TenancyId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.TenancyId);
        });

        // ========== NOTIFICATION TEMPLATE (Org-Scoped Entity) ==========
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SubjectTemplate).HasMaxLength(256);
            entity.Property(e => e.BodyTemplate).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== NOTIFICATION (Org-Scoped Entity) ==========
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ToAddress).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Subject).HasMaxLength(256);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderMessageId).HasMaxLength(256);

            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Tenancy)
                .WithMany()
                .HasForeignKey(e => e.TenancyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Template)
                .WithMany()
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.TenancyId);
            entity.HasIndex(e => new { e.Status, e.ScheduledFor });
        });

        // ========== NOTIFICATION ATTEMPT (Org-Scoped Entity) ==========
        modelBuilder.Entity<NotificationAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrganizationId).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProviderMessageId).HasMaxLength(256);

            entity.HasOne(e => e.Notification)
                .WithMany(n => n.Attempts)
                .HasForeignKey(e => e.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.NotificationId);
        });

        // ========== AUDIT LOG (Global Entity, not org-filtered -- see visibility note on AuditLog) ==========
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorRole).IsRequired().HasMaxLength(50);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetType).HasMaxLength(100);
            entity.Property(e => e.TargetId).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(128);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.OrganizationId, e.CreatedAt });
            entity.HasIndex(e => e.ActorUserId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.SupportSessionId);
        });

        // ========== SUPPORT SESSION (Global Entity, not org-filtered -- see class doc) ==========
        modelBuilder.Entity<SupportSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(1024);
            entity.Property(e => e.EndedReason).HasMaxLength(50);
            entity.Property(e => e.StartedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => new { e.AdminUserId, e.OrganizationId });
            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== SUPPORT ACCESS CODE (Global Entity, not org-filtered -- see class doc) ==========
        modelBuilder.Entity<SupportAccessCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(12);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.OrganizationId);
        });

        // ========== GLOBAL QUERY FILTERS (Multi-Tenancy Highway Guardrail) ==========
        // This is the CRITICAL SECURITY LINE: all org-scoped entity queries automatically filtered by CurrentOrgId.
        // If CurrentOrgId is null, the query returns nothing (user not in an org context).

        modelBuilder.Entity<House>()
            .HasQueryFilter(h => h.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<Tenant>()
            .HasQueryFilter(t => t.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<Tenancy>()
            .HasQueryFilter(t => t.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<Expense>()
            .HasQueryFilter(e => e.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => d.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<RentPayment>()
            .HasQueryFilter(r => r.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<DepositPayment>()
            .HasQueryFilter(d => d.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<DepositReturn>()
            .HasQueryFilter(d => d.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<Notification>()
            .HasQueryFilter(n => n.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<NotificationTemplate>()
            .HasQueryFilter(t => t.OrganizationId == _tenantContext.CurrentOrgId);
        modelBuilder.Entity<NotificationAttempt>()
            .HasQueryFilter(a => a.OrganizationId == _tenantContext.CurrentOrgId);

        // NOTE: Organization and OrganizationMember are intentionally NOT filtered here.
        // Users must be able to list their orgs and org members without the filter.
        // Access control is enforced at the controller/service layer.
    }
}

