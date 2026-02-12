namespace Mockapala.Result;

/// <summary>
/// In-memory container of generated entities by type. Use Get&lt;T&gt;() for type-safe access.
/// </summary>
public sealed class GeneratedData : IGeneratedData
{
    private readonly Dictionary<Type, IReadOnlyList<object>> _byType = new();

    internal GeneratedData() { }

    internal void Set(Type entityType, IReadOnlyList<object> list)
    {
        _byType[entityType] = list;
    }

    /// <inheritdoc />
    public IReadOnlyList<T> Get<T>()
    {
        if (_byType.TryGetValue(typeof(T), out var list))
            return list.Cast<T>().ToList();
        throw new KeyNotFoundException($"No generated data for entity type {typeof(T).Name}. Ensure it was registered in the schema and a count was specified.");
    }

    /// <inheritdoc />
    public IReadOnlyList<object> Get(Type entityType)
    {
        if (_byType.TryGetValue(entityType, out var list))
            return list;
        throw new KeyNotFoundException($"No generated data for entity type {entityType.Name}. Ensure it was registered in the schema and a count was specified.");
    }
}
