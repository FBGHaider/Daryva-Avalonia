using Daryva.Api.Data;
using Daryva.Api.Repositories;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services;
using Daryva.Api.Services.Interfaces;
using Daryva.Api.Services.Seed;
using Daryva.Api.Services.Seed.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .ToArray() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// HTTP context accessor for tenant context
builder.Services.AddHttpContextAccessor();

// Tenant context (scoped to request)
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Repositories + unit of work (services must not depend on AppDbContext directly)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IAuthRefreshTokenRepository, AuthRefreshTokenRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IOrganizationMemberRepository, OrganizationMemberRepository>();
builder.Services.AddScoped<ISupportSessionRepository, SupportSessionRepository>();

// Business logic services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IMeService, MeService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRentLedgerService, RentLedgerService>();
builder.Services.AddScoped<IHouseService, HouseService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IDataSeeder, DataSeeder>();
builder.Services.AddScoped<IBulkImportService, BulkImportService>();
builder.Services.AddScoped<IOrganizationSyncService, OrganizationSyncService>();
builder.Services.AddScoped<IPlatformAdminBootstrapper, PlatformAdminBootstrapper>();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT Bearer Authentication
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrgResourceAuthorizationHandler>();

// Add authentication with either external authority or local signing key.
if (!string.IsNullOrEmpty(jwtOptions.Authority))
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = jwtOptions.Authority;
            options.Audience = jwtOptions.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ClockSkew = TimeSpan.Zero
            };
        });
}
else
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters when Jwt:Authority is not set.");
    }

    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
}

// Rate limiting. "auth" is a strict per-IP policy for the auth endpoints most worth throttling
// (login/register/refresh/resend-verification/forgot-password/verify-email/reset-password) --
// defense in depth on top of the per-account lockout AuthService already enforces for login.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            message = "Too many attempts. Please wait before trying again."
        }, cancellationToken);
    };

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Looser general policy for the rest of the API, to blunt scraping/abuse. Applies to every
    // request (stacks with "auth" on top for those endpoints, not instead of it) except health
    // checks and swagger, which get polled/loaded far more often than any real abuse pattern.
    // Partitioned by user identity when authenticated (fair per-account budget across a session
    // opening several requests at once), falling back to IP for anonymous requests.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (path == "/health" || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("exempt");

        var partitionKey = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey,
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration failed. Ensure the database is reachable and the connection string is correct. Error: {Message}", ex.Message);
        throw;
    }

    // Grants IsPlatformAdmin to Admin:BootstrapEmails, idempotently. No-op if unset.
    var platformAdminBootstrapper = scope.ServiceProvider.GetRequiredService<IPlatformAdminBootstrapper>();
    await platformAdminBootstrapper.BootstrapAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseRateLimiter();

// Return 500 with JSON body (error, detail, inner) for any unhandled exception
app.UseMiddleware<ExceptionHandlerMiddleware>();

// Auth:Mode = "Dev" or DevAuth:Enabled = true (used for seeding and for fallback below).
// In non-Development environments, DevAuth is never enabled (no dev@local leakage).
var authMode = app.Configuration.GetValue<string>("Auth:Mode");
var devAuthConfigEnabled = string.Equals(authMode, "Dev", StringComparison.OrdinalIgnoreCase)
    || app.Configuration.GetValue<bool>("DevAuth:Enabled");
var devAuthEnabled = app.Environment.IsDevelopment() && devAuthConfigEnabled;

// Authentication first so Clerk/JWT can set User; DevAuth runs after as fallback when no token
app.UseAuthentication();

if (devAuthEnabled)
{
    app.UseMiddleware<DevAuthMiddleware>();
}

// Tenant context must run before UseAuthorization: permission-policy checks (PermissionAuthorizationHandler)
// read ITenantContext.CurrentRole/IsPlatformAdmin, which this middleware resolves. It only needs
// HttpContext.User (set by UseAuthentication/DevAuth above), not UseAuthorization's own output.
app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint (public, no auth required)
app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        await db.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { status = "unhealthy", error = ex.Message });
    }
});

// Seed sample data on startup (if DevAuth is enabled)
if (devAuthEnabled)
{
    using var scope = app.Services.CreateScope();
    var dataSeeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await dataSeeder.SeedIfNeededAsync();

    // Sync organization memberships for dev user
    // This ensures dev user has access to all organizations with data
    var orgSyncService = scope.ServiceProvider.GetRequiredService<IOrganizationSyncService>();
    var devUserId = app.Configuration.GetValue<string>("DevAuth:UserId") ?? "dev-user-1";
    await orgSyncService.SyncUserOrgMembershipsAsync(devUserId);
}

app.Run();
