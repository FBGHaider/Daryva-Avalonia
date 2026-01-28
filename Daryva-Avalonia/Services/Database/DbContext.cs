using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Implementation of IDbContext using SQL Server and Dapper.
    /// </summary>
    public class DbContext : IDbContext
    {
        private readonly string _connectionString;
        private IDbConnection? _connection;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the DbContext class.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        public DbContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Gets the database connection.
        /// </summary>
        public IDbConnection Connection
        {
            get
            {
                if (_connection == null)
                {
                    _connection = new SqlConnection(_connectionString);
                }
                return _connection;
            }
        }

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        public void OpenConnection()
        {
            // Handle all non-open states
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                // If connection exists but is in a bad state, dispose and recreate it
                if (_connection != null)
                {
                    try
                    {
                        if (_connection.State != ConnectionState.Closed)
                        {
                            _connection.Close();
                        }
                        _connection.Dispose();
                    }
                    catch { }
                    _connection = null;
                }
                
                // Create a fresh connection
                _connection = new SqlConnection(_connectionString);
                
                // Open the connection and wait until it's fully open
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                    
                    // Wait until connection is fully open (not just connecting)
                    int retries = 0;
                    while (_connection.State == ConnectionState.Connecting && retries < 10)
                    {
                        System.Threading.Thread.Sleep(50);
                        retries++;
                    }
                    
                    // If still not open after retries, throw exception
                    if (_connection.State != ConnectionState.Open)
                    {
                        throw new InvalidOperationException($"Connection failed to open. Current state: {_connection.State}");
                    }
                }
            }
        }

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        public void CloseConnection()
        {
            if (Connection.State != ConnectionState.Closed)
            {
                Connection.Close();
            }
        }

        /// <summary>
        /// Executes a command and returns the number of rows affected.
        /// </summary>
        public int ExecuteNonQuery(string sql, object? parameters = null)
        {
            try
            {
                OpenConnection();
                return Connection.Execute(sql, parameters);
            }
            finally
            {
                // Connection pooling handles cleanup, but ensure it's in good state
                if (_connection?.State == ConnectionState.Broken)
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Executes a query and returns the first result.
        /// </summary>
        public T? ExecuteScalar<T>(string sql, object? parameters = null)
        {
            try
            {
                OpenConnection();
                return Connection.ExecuteScalar<T>(sql, parameters);
            }
            finally
            {
                if (_connection?.State == ConnectionState.Broken)
                {
                    CloseConnection();
                }
            }
        }

        /// <summary>
        /// Executes a query and returns a list of results.
        /// </summary>
        public IEnumerable<T> Query<T>(string sql, object? parameters = null)
        {
            // Use a short-lived connection for read queries to avoid issues
            // with shared connection state (Connecting, Broken, etc.).
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            var results = connection.Query<T>(sql, parameters).ToList();
            return results;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the DbContext and optionally releases the managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the DbContext and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CloseConnection();
                    _connection?.Dispose();
                    _connection = null;
                }
                _disposed = true;
            }
        }
    }
}
