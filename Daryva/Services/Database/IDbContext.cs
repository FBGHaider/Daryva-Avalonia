using System.Data;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Interface for database context operations.
    /// </summary>
    public interface IDbContext : IDisposable
    {
        /// <summary>
        /// Gets the database connection.
        /// </summary>
        IDbConnection Connection { get; }

        /// <summary>
        /// Opens the database connection.
        /// </summary>
        void OpenConnection();

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        void CloseConnection();

        /// <summary>
        /// Executes a command and returns the number of rows affected.
        /// </summary>
        int ExecuteNonQuery(string sql, object? parameters = null);

        /// <summary>
        /// Executes a query and returns the first result.
        /// </summary>
        T? ExecuteScalar<T>(string sql, object? parameters = null);

        /// <summary>
        /// Executes a query and returns a list of results.
        /// </summary>
        IEnumerable<T> Query<T>(string sql, object? parameters = null);
    }
}
