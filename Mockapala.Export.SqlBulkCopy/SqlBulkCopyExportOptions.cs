using Microsoft.Data.SqlClient;

namespace Mockapala.Export.SqlBulkCopy;

/// <summary>
/// Options for SqlBulkCopy export to SQL Server.
/// </summary>
public sealed class SqlBulkCopyExportOptions
{
    /// <summary>
    /// Resolves the table name for an entity type. Default: type name (e.g. Company -> "Company").
    /// </summary>
    public Func<Type, string>? TableNameResolver { get; set; }

    /// <summary>
    /// Quote identifiers with brackets for SQL Server. Default: true.
    /// </summary>
    public bool QuoteIdentifiers { get; set; } = true;

    /// <summary>
    /// Number of rows in each batch (0 = use SqlBulkCopy default). Default: 0.
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>
    /// When true, use IDataReader to stream rows (lower memory). When false, build a DataTable per entity type. Default: true.
    /// </summary>
    public bool UseDataReader { get; set; } = true;

    /// <summary>
    /// Options for the SqlBulkCopy operation. Default: KeepIdentity (so generated entity IDs are preserved).
    /// </summary>
    public SqlBulkCopyOptions BulkCopyOptions { get; set; } = SqlBulkCopyOptions.KeepIdentity;

    internal string GetTableName(Type entityType)
    {
        var name = TableNameResolver != null ? TableNameResolver(entityType) : entityType.Name;
        return QuoteIdentifiers ? $"[{name}]" : name;
    }

    internal string QuoteColumn(string name) => QuoteIdentifiers ? $"[{name}]" : name;
}
