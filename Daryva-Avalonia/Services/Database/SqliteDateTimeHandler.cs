using System;
using System.Data;
using Dapper;

namespace Daryva.Services.Database
{
    /// <summary>
    /// Custom Dapper type handler for SQLite TEXT date columns (nullable).
    /// Handles NULL values and converts TEXT dates to DateTime?.
    /// </summary>
    public class SqliteDateTimeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            parameter.Value = value?.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        public override DateTime? Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is DateTime dt)
                return dt;

            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str) || str.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (DateTime.TryParse(str, out var parsed))
                    return parsed;
            }

            return null;
        }
    }

    /// <summary>
    /// Custom Dapper type handler for SQLite TEXT date columns (non-nullable).
    /// Handles TEXT dates and converts to DateTime.
    /// </summary>
    public class SqliteDateTimeNonNullableHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        public override DateTime Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return DateTime.MinValue;

            if (value is DateTime dt)
                return dt;

            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str) || str.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    return DateTime.MinValue;

                if (DateTime.TryParse(str, out var parsed))
                    return parsed;
            }

            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Custom Dapper type handler for SQLite nullable integer columns.
    /// Handles NULL values and converts to int?.
    /// </summary>
    public class SqliteNullableIntHandler : SqlMapper.TypeHandler<int?>
    {
        public override void SetValue(IDbDataParameter parameter, int? value)
        {
            parameter.Value = value ?? (object)DBNull.Value;
        }

        public override int? Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            if (value is int i)
                return i;

            if (value is long l)
                return (int)l;

            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str) || str.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (int.TryParse(str, out var parsed))
                    return parsed;
            }

            return null;
        }
    }

    /// <summary>
    /// Custom Dapper type handler for SQLite non-nullable integer columns.
    /// Handles NULL values by returning default (0) or a specified default value.
    /// </summary>
    public class SqliteIntHandler : SqlMapper.TypeHandler<int>
    {
        public override void SetValue(IDbDataParameter parameter, int value)
        {
            parameter.Value = value;
        }

        public override int Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return 0; // Default value for non-nullable int

            if (value is int i)
                return i;

            if (value is long l)
                return (int)l;

            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str) || str.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    return 0;

                if (int.TryParse(str, out var parsed))
                    return parsed;
            }

            return 0;
        }
    }

    /// <summary>
    /// Custom Dapper type handler for SQLite boolean columns (stored as INTEGER 0/1).
    /// Handles NULL values and string "NULL" by returning false.
    /// </summary>
    public class SqliteBoolHandler : SqlMapper.TypeHandler<bool>
    {
        public override void SetValue(IDbDataParameter parameter, bool value)
        {
            parameter.Value = value ? 1 : 0;
        }

        public override bool Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return false;

            if (value is bool b)
                return b;

            if (value is int i)
                return i != 0;

            if (value is long l)
                return l != 0;

            if (value is string str)
            {
                if (string.IsNullOrWhiteSpace(str) || str.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    return false;

                // Try parsing as integer first (0/1)
                if (int.TryParse(str, out var intVal))
                    return intVal != 0;

                // Try parsing as boolean
                if (bool.TryParse(str, out var boolVal))
                    return boolVal;
            }

            return false;
        }
    }
}
