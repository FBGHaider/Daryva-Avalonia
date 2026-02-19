using System.Data;
using Microsoft.Data.Sqlite;
using Dapper;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Implementation of IDbContext using SQLite and Dapper.
    /// Cross-platform compatible.
    /// </summary>
    public class DbContext : IDbContext
    {
        private readonly string _connectionString;
        private IDbConnection? _connection;
        private bool _disposed = false;

        static DbContext()
        {
            // Register custom type handlers for SQLite date, integer, and boolean handling
            SqlMapper.AddTypeHandler(new SqliteDateTimeHandler());
            SqlMapper.AddTypeHandler(new SqliteDateTimeNonNullableHandler());
            SqlMapper.AddTypeHandler(new SqliteNullableIntHandler());
            SqlMapper.AddTypeHandler(new SqliteIntHandler());
            SqlMapper.AddTypeHandler(new SqliteBoolHandler());
            SqlMapper.AddTypeHandler(new SqliteDecimalHandler());
        }

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
                    _connection = new SqliteConnection(_connectionString);
                }
                return _connection;
            }
        }

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        public void OpenConnection()
        {
            if (_connection == null)
            {
                _connection = new SqliteConnection(_connectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                _connection.Open();
            }
        }

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        public void CloseConnection()
        {
            if (_connection != null && _connection.State != ConnectionState.Closed)
            {
                _connection.Close();
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
            // Use a short-lived connection for read queries
            using var connection = new SqliteConnection(_connectionString);
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
