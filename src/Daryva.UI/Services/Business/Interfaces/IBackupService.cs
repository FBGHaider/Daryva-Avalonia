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
        /// Restores backup data from a JSON file into the current organization.
        /// </summary>
        Task RestoreBackupAsync(string backupFilePath);

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
