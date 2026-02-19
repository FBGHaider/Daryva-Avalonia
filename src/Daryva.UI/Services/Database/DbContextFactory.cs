using Daryva.Services;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Factory implementation for creating IDbContext instances.
    /// </summary>
    public class DbContextFactory : IDbContextFactory
    {
        private readonly IConfigurationService _configurationService;

        /// <summary>
        /// Initializes a new instance of the DbContextFactory class.
        /// </summary>
        /// <param name="configurationService">The configuration service for getting the local storage connection string.</param>
        public DbContextFactory(IConfigurationService configurationService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        /// <summary>
        /// Creates a new IDbContext instance using the local storage connection string from configuration.
        /// </summary>
        public IDbContext CreateDbContext()
        {
            var connectionString = _configurationService.GetConnectionString();
            return new DbContext(connectionString);
        }
    }
}
