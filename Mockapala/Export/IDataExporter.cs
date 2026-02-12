using Mockapala.Result;

namespace Mockapala.Export;

/// <summary>
/// Exports generated data to a stream (e.g. SQL INSERTs, Excel, JSON).
/// </summary>
public interface IDataExporter
{
    /// <summary>
    /// Exports the generated data to the given stream.
    /// </summary>
    void Export(IGeneratedData data, Stream output);
}
