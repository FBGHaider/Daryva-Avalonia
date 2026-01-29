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
            // Application data directory
            var appDataBase = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _appData = Path.Combine(appDataBase, "Daryva");
            
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
