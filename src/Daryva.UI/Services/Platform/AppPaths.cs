using System.IO;

namespace Daryva.Services.Platform
{
    /// <summary>
    /// Cross-platform implementation of IAppPaths.
    /// </summary>
    public class AppPaths : IAppPaths
    {
        private readonly string _appData;
        private readonly string _logs;
        private readonly string _exports;
        private readonly string _database;

        public AppPaths()
        {
            // Application data directory. DARYVA_PROFILE lets multiple instances run side by side
            // with isolated auth session / local db / settings -- e.g. testing an admin account and
            // a landlord account at the same time (set DARYVA_PROFILE=admin before launching one of them).
            var appDataBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var profile = Environment.GetEnvironmentVariable("DARYVA_PROFILE");
            var folderName = string.IsNullOrWhiteSpace(profile) ? "Daryva" : $"Daryva-{profile}";
            _appData = Path.Combine(appDataBase, folderName);
            
            // Ensure directories exist
            if (!Directory.Exists(_appData))
            {
                Directory.CreateDirectory(_appData);
            }

            // Logs directory
            _logs = Path.Combine(_appData, "Logs");
            if (!Directory.Exists(_logs))
            {
                Directory.CreateDirectory(_logs);
            }

            // Database directory
            _database = Path.Combine(_appData, "Database");
            if (!Directory.Exists(_database))
            {
                Directory.CreateDirectory(_database);
            }

            // Exports directory (MyDocuments\Daryva Exports)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _exports = Path.Combine(documentsPath, "Daryva Exports");
            if (!Directory.Exists(_exports))
            {
                Directory.CreateDirectory(_exports);
            }
        }

        public string AppData => _appData;
        public string Logs => _logs;
        public string Exports => _exports;
        public string Database => _database;
    }
}
