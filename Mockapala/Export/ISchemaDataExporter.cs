using Mockapala.Result;
using Mockapala.Schema;

namespace Mockapala.Export;

/// <summary>
/// Exports generated data to a stream using schema (entity order and metadata).
/// Implemented by schema-aware exporters such as SQL Server script export.
/// </summary>
public interface ISchemaDataExporter
{
    /// <summary>
    /// Exports the generated data to the given stream in schema order (dependencies first).
    /// </summary>
    void Export(ISchema schema, IGeneratedData data, Stream output);
}
