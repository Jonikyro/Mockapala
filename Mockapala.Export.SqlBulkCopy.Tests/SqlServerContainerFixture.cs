using Microsoft.Data.SqlClient;
using Mockapala.Export;
using Mockapala.Schema;
using Testcontainers.MsSql;
using Xunit;

namespace Mockapala.Export.SqlBulkCopy.Tests;

/// <summary>
/// Shared fixture that manages a SQL Server Docker container for the test run.
/// </summary>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Container has not been started.");

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync().AsTask();
    }

    /// <summary>
    /// Creates tables for all entities in the schema and drops any existing user tables first.
    /// </summary>
    public void ResetDatabase(ISchema schema)
    {
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();

        DropAllUserTables(connection);

        foreach (var entityType in schema.GenerationOrder)
        {
            var definition = schema.Entities.FirstOrDefault(e => e.EntityType == entityType);
            var exportable = ExportableProperty.GetExportableProperties(entityType, definition);
            if (exportable.Count == 0)
                continue;

            var tableName = entityType.Name;
            CreateTable(connection, tableName, exportable);
        }
    }

    private static void DropAllUserTables(SqlConnection connection)
    {
        // Drop all tables respecting foreign key constraints
        using var cmd = new SqlCommand(
            """
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += 'ALTER TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name)
                + ' DROP CONSTRAINT ' + QUOTENAME(f.name) + ';' + CHAR(13)
            FROM sys.foreign_keys f
            INNER JOIN sys.tables t ON f.parent_object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id;
            EXEC sp_executesql @sql;

            SET @sql = N'';
            SELECT @sql += 'DROP TABLE ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ';' + CHAR(13)
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE t.type = 'U';
            EXEC sp_executesql @sql;
            """, connection);
        cmd.ExecuteNonQuery();
    }

    private static void CreateTable(SqlConnection connection, string tableName, IReadOnlyList<ExportableProperty> properties)
    {
        var columns = string.Join(", ", properties.Select(p =>
            $"[{p.Property.Name}] {GetSqlServerColumnType(p.EffectiveType)}"));

        using var cmd = new SqlCommand($"CREATE TABLE [{tableName}] ({columns});", connection);
        cmd.ExecuteNonQuery();
    }

    private static string GetSqlServerColumnType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(int)) return "INT";
        if (t == typeof(long)) return "BIGINT";
        if (t == typeof(short)) return "SMALLINT";
        if (t == typeof(byte)) return "TINYINT";
        if (t == typeof(bool)) return "BIT";
        if (t == typeof(float)) return "REAL";
        if (t == typeof(double)) return "FLOAT";
        if (t == typeof(decimal)) return "DECIMAL(18,6)";
        if (t == typeof(string)) return "NVARCHAR(MAX)";
        if (t == typeof(Guid)) return "UNIQUEIDENTIFIER";
        if (t == typeof(DateTime)) return "DATETIME2";
        if (t == typeof(DateTimeOffset)) return "DATETIMEOFFSET";
        if (t == typeof(TimeSpan)) return "TIME";
        if (t == typeof(byte[])) return "VARBINARY(MAX)";
        if (t.IsEnum) return "INT";

        return "NVARCHAR(MAX)";
    }
}

[CollectionDefinition(Name)]
public class SqlServerTestCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SqlServer";
}
