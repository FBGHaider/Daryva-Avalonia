namespace Daryva.Api.Services.Seed;

/// <summary>
/// Service for seeding sample data into the database.
/// Development-only.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Seed sample organization and houses for the dev user if they don't already exist.
    /// </summary>
    Task SeedIfNeededAsync(CancellationToken cancellationToken = default);
}
