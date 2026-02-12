using System.Reflection;
using Microsoft.Data.Sqlite;
using Mockapala.Export;
using Mockapala.Result;
using Mockapala.Schema;

namespace Mockapala.Export.Sqlite;

/// <summary>
/// Exports generated data to a SQLite database using parameterized INSERTs in a transaction.
/// </summary>
public sealed class SqliteExporter : ISchemaDataExporter
{
    private readonly SqliteExportOptions _options;

    public SqliteExporter(SqliteExportOptions? options = null)
    {
        _options = options ?? new SqliteExportOptions();
    }

    /// <inheritdoc />
    public void Export(ISchema schema, IGeneratedData data, Stream output)
    {
        throw new NotSupportedException(
            "SQLite exporter writes to a database. Use ExportToDatabase(ISchema, IGeneratedData, string connectionString) instead.");
    }

    /// <summary>
    /// Exports generated data into a SQLite database using the given connection string.
    /// </summary>
    public void ExportToDatabase(ISchema schema, IGeneratedData data, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        ApplyPragmas(connection);

        using var transaction = connection.BeginTransaction();

        foreach (var entityType in schema.GenerationOrder)
        {
            var list = data.Get(entityType);
            if (list.Count == 0)
                continue;

            var properties = GetScalarProperties(entityType);
            if (properties.Count == 0)
                continue;

            var tableName = _options.GetTableName(entityType);

            if (_options.CreateTables)
                CreateTable(connection, tableName, properties);

            InsertRows(connection, tableName, properties, list);
        }

        transaction.Commit();
    }

    /// <summary>
    /// Convenience overload: exports generated data into a SQLite database file.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <param name="data">The generated data.</param>
    /// <param name="filePath">Path to the SQLite database file.</param>
    /// <param name="createIfMissing">When true (default), creates the file if it doesn't exist.</param>
    public void ExportToDatabase(ISchema schema, IGeneratedData data, string filePath, bool createIfMissing = true)
    {
        var mode = createIfMissing ? "ReadWriteCreate" : "ReadWrite";
        var connectionString = $"Data Source={filePath};Mode={mode}";
        ExportToDatabase(schema, data, connectionString);
    }

    private void ApplyPragmas(SqliteConnection connection)
    {
        if (_options.UseWalMode)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
        }
    }

    private void CreateTable(SqliteConnection connection, string tableName, IReadOnlyList<PropertyInfo> properties)
    {
        var columns = string.Join(", ", properties.Select(p =>
            $"{_options.QuoteColumn(p.Name)} {GetSqliteColumnType(p.PropertyType)}"));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} ({columns});";
        cmd.ExecuteNonQuery();
    }

    private void InsertRows(SqliteConnection connection, string tableName, IReadOnlyList<PropertyInfo> properties, IReadOnlyList<object> entities)
    {
        var columnNames = string.Join(", ", properties.Select(p => _options.QuoteColumn(p.Name)));
        var paramNames = string.Join(", ", properties.Select((_, i) => $"$p{i}"));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} ({columnNames}) VALUES ({paramNames});";

        // Pre-create parameters
        var parameters = new SqliteParameter[properties.Count];
        for (var i = 0; i < properties.Count; i++)
        {
            var param = new SqliteParameter($"$p{i}", GetSqliteParamType(properties[i].PropertyType));
            cmd.Parameters.Add(param);
            parameters[i] = param;
        }

        cmd.Prepare();

        foreach (var entity in entities)
        {
            for (var i = 0; i < properties.Count; i++)
            {
                var value = properties[i].GetValue(entity);
                parameters[i].Value = value ?? DBNull.Value;
            }
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Returns the public, readable+writable properties with scalar types for the given entity type.
    /// </summary>
    internal static IReadOnlyList<PropertyInfo> GetScalarProperties(Type entityType)
    {
        return entityType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsScalarType(p.PropertyType))
            .ToList();
    }

    private static bool IsScalarType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan)
            || t == typeof(Guid)
            || t == typeof(byte[]);
    }

    private static string GetSqliteColumnType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(bool) || t.IsEnum)
            return "INTEGER";

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return "REAL";

        if (t == typeof(byte[]))
            return "BLOB";

        // string, DateTime, DateTimeOffset, TimeSpan, Guid, and anything else -> TEXT
        return "TEXT";
    }

    private static SqliteType GetSqliteParamType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
            || t == typeof(bool) || t.IsEnum)
            return SqliteType.Integer;

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return SqliteType.Real;

        if (t == typeof(byte[]))
            return SqliteType.Blob;

        return SqliteType.Text;
    }
}
