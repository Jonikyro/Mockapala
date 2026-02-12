namespace Mockapala.Result;

/// <summary>
/// Type-safe access to generated entity lists.
/// </summary>
public interface IGeneratedData
{
    /// <summary>
    /// Returns the list of generated instances for entity type T.
    /// </summary>
    IReadOnlyList<T> Get<T>();

    /// <summary>
    /// Returns the list of generated instances for the given entity type (non-generic).
    /// Useful for exporters that iterate by Type without knowing the generic parameter.
    /// </summary>
    IReadOnlyList<object> Get(Type entityType);
}
