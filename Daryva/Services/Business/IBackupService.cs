namespace Daryva.Services.Business
{
    /// <summary>
    /// Service interface for database backup operations.
    /// </summary>
    public interface IBackupService
    {
        /// <summary>
        /// Creates a backup of the database.
        /// </summary>
        Task<string> CreateBackupAsync(string? backupPath = null);

        /// <summary>
        /// Gets the default backup location.
        /// </summary>
        string GetDefaultBackupLocation();

        /// <summary>
        /// Gets the database name.
        /// </summary>
        string GetDatabaseName();
    }
}
