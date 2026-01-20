namespace FBGRentora.Services.Database
{
    /// <summary>
    /// Factory for creating IDbContext instances.
    /// </summary>
    public interface IDbContextFactory
    {
        /// <summary>
        /// Creates a new IDbContext instance.
        /// </summary>
        IDbContext CreateDbContext();
    }
}
