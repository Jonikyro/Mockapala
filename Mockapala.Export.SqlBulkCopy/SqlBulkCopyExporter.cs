using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Mockapala.Export;
using Mockapala.Result;
using Mockapala.Schema;

namespace Mockapala.Export.SqlBulkCopy;

/// <summary>
/// Exports generated data to SQL Server using SqlBulkCopy for fast bulk insert of large volumes.
/// </summary>
public sealed class SqlBulkCopyExporter : ISchemaDataExporter
{
    private readonly SqlBulkCopyExportOptions _options;

    public SqlBulkCopyExporter(SqlBulkCopyExportOptions? options = null)
    {
        _options = options ?? new SqlBulkCopyExportOptions();
    }

    /// <inheritdoc />
    public void Export(ISchema schema, IGeneratedData data, Stream output)
    {
        throw new NotSupportedException(
            "SqlBulkCopy exporter writes to a database. Use ExportToDatabase(ISchema, IGeneratedData, string connectionString) instead.");
    }

    /// <summary>
    /// Bulk-copies generated data into a SQL Server database using the given connection string.
    /// </summary>
    public void ExportToDatabase(ISchema schema, IGeneratedData data, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required.", nameof(connectionString));

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        foreach (var entityType in schema.GenerationOrder)
        {
            var list = data.Get(entityType);
            if (list.Count == 0)
                continue;

            var definition = schema.Entities.FirstOrDefault(e => e.EntityType == entityType);
            var exportable = ExportableProperty.GetExportableProperties(entityType, definition);
            if (exportable.Count == 0)
                continue;

            var tableName = _options.GetTableName(entityType);

            if (_options.UseDataReader)
            {
                using var reader = new EntityDataReader(list, exportable);
                using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection, _options.BulkCopyOptions, null)
                {
                    DestinationTableName = tableName
                };
                if (_options.BatchSize > 0)
                    bulkCopy.BatchSize = _options.BatchSize;
                bulkCopy.WriteToServer(reader);
            }
            else
            {
                var dataTable = BuildDataTable(list, exportable);
                using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection, _options.BulkCopyOptions, null)
                {
                    DestinationTableName = tableName
                };
                if (_options.BatchSize > 0)
                    bulkCopy.BatchSize = _options.BatchSize;
                bulkCopy.WriteToServer(dataTable);
            }
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

    private static DataTable BuildDataTable(IReadOnlyList<object> entities, IReadOnlyList<ExportableProperty> properties)
    {
        var table = new DataTable();
        foreach (var p in properties)
        {
            var colType = Nullable.GetUnderlyingType(p.EffectiveType) ?? p.EffectiveType;
            table.Columns.Add(p.Property.Name, colType);
        }
        foreach (var entity in entities)
        {
            var row = table.NewRow();
            for (var i = 0; i < properties.Count; i++)
            {
                var value = properties[i].GetValue(entity);
                row[i] = value ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }
        return table;
    }
}
