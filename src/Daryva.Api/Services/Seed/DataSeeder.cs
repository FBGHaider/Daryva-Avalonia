using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Services.Seed.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services.Seed;

/// <summary>
/// Seed sample data for development.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(AppDbContext dbContext, ILogger<DataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedIfNeededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we already have sample data (org for dev user)
            const string devUserId = "dev-user-1";

            var existingOrg = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    o => o.Members.Any(m => m.UserId == devUserId),
                    cancellationToken);

            if (existingOrg != null)
            {
                _logger.LogInformation("✓ Sample data already exists. Org: {OrgName} ({OrgId})", existingOrg.Name, existingOrg.Id);
                return;
            }

            _logger.LogInformation("Seeding sample data for dev user...");

            // Create dev organization
            var devOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Dev Property Management",
                CreatedAt = DateTime.UtcNow,
            };

            // Create dev user as owner
            var devMember = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = devOrg.Id,
                UserId = devUserId,
                Email = "dev@local",
                Role = OrganizationMember.Roles.Landlord,
                IsPrimaryOwner = true,
                JoinedAt = DateTime.UtcNow,
            };

            devOrg.Members.Add(devMember);

            // Create sample houses
            var house1 = new House
            {
                Id = Guid.NewGuid(),
                OrganizationId = devOrg.Id,
                Name = "Main St Apartment A",
                AddressLine1 = "123 Main Street",
                AddressLine2 = "Apt 1A",
                City = "New York",
                Postcode = "10001",
                CreatedAt = DateTime.UtcNow,
            };

            var house2 = new House
            {
                Id = Guid.NewGuid(),
                OrganizationId = devOrg.Id,
                Name = "Park Ave Townhouse",
                AddressLine1 = "456 Park Avenue",
                AddressLine2 = null,
                City = "New York",
                Postcode = "10002",
                CreatedAt = DateTime.UtcNow,
            };

            var house3 = new House
            {
                Id = Guid.NewGuid(),
                OrganizationId = devOrg.Id,
                Name = "Brooklyn Loft",
                AddressLine1 = "789 Brooklyn Street",
                AddressLine2 = "Suite 200",
                City = "Brooklyn",
                Postcode = "11201",
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.Organizations.Add(devOrg);
            _dbContext.Houses.AddRange(house1, house2, house3);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("✓ Seeded sample data:");
            _logger.LogInformation("  Organization: {OrgName} (ID: {OrgId})", devOrg.Name, devOrg.Id);
            _logger.LogInformation("  Member: {Email} (Role: {Role})", devMember.Email, devMember.Role);
            _logger.LogInformation("  Houses: {Count}", new[] { house1, house2, house3 }.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding sample data");
            throw;
        }
    }
}
