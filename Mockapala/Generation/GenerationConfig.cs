using Mockapala.Result;

namespace Mockapala.Generation;

/// <summary>
/// Specifies the count behavior for an entity type.
/// </summary>
/// <param name="Count">How many entities to generate.</param>
/// <param name="Flexible">When true, entities that fail relation resolution are discarded instead of throwing.</param>
/// <param name="Min">Minimum number of entities that must survive. Only meaningful when Flexible is true.</param>
internal record CountSpec(int Count, bool Flexible, int Min);

/// <summary>
/// Configuration for a generation run: counts per entity type, optional seed, prefill data, and post-processing steps.
/// </summary>
public sealed class GenerationConfig
{
    private readonly Dictionary<Type, CountSpec> _countSpecs = new();
    private readonly Dictionary<Type, IReadOnlyList<object>> _prefill = new();
    private readonly List<Action<IGeneratedData>> _postProcessSteps = new();
    private int? _seed;

    internal GenerationConfig() { }

    /// <summary>
    /// Sets an exact count of instances to generate for entity type T.
    /// If any entity cannot resolve its required relations, generation throws.
    /// Ignored when Prefill is set for the same type.
    /// </summary>
    public GenerationConfig Count<T>(int count) where T : class
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
        _countSpecs[typeof(T)] = new CountSpec(count, Flexible: false, Min: count);
        return this;
    }

    /// <summary>
    /// Sets an ideal (flexible) count of instances to generate for entity type T.
    /// Generates up to <paramref name="count"/> entities; entities that cannot resolve a required
    /// relation are silently discarded. If the number of survivors falls below <paramref name="min"/>,
    /// generation throws with a clear message.
    /// Ignored when Prefill is set for the same type.
    /// </summary>
    /// <param name="count">Maximum number of entities to generate (must be > 0).</param>
    /// <param name="min">Minimum number of entities that must survive relation resolution (default 1).</param>
    public GenerationConfig IdealCount<T>(int count, int min = 1) where T : class
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than zero.");
        if (min < 0)
            throw new ArgumentOutOfRangeException(nameof(min), "Min must be non-negative.");
        if (min > count)
            throw new ArgumentOutOfRangeException(nameof(min), $"Min ({min}) must be less than or equal to count ({count}).");
        _countSpecs[typeof(T)] = new CountSpec(count, Flexible: true, Min: min);
        return this;
    }

    /// <summary>
    /// Sets the random seed for reproducible generation.
    /// </summary>
    public GenerationConfig Seed(int seed)
    {
        _seed = seed;
        return this;
    }

    /// <summary>
    /// Supplies a fixed list of instances for entity type T instead of generating them.
    /// Instances must already have their key values set. Count for this type is ignored.
    /// Relation resolution still runs, so prefilled entities can have FKs resolved to other entities.
    /// </summary>
    public GenerationConfig Prefill<T>(IReadOnlyList<T> instances) where T : class
    {
        if (instances == null)
            throw new ArgumentNullException(nameof(instances));
        _prefill[typeof(T)] = instances.Cast<object>().ToList();
        return this;
    }

    /// <summary>
    /// Adds a post-processing step that runs after all entities and relations are resolved.
    /// Use to compute derived fields (e.g. Order.Total from OrderLines) or validate invariants.
    /// Steps run in the order they are added.
    /// </summary>
    public GenerationConfig PostProcess(Action<IGeneratedData> step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));
        _postProcessSteps.Add(step);
        return this;
    }

    internal int GetCount(Type entityType) =>
        _countSpecs.TryGetValue(entityType, out var spec) ? spec.Count : 0;

    internal CountSpec? GetCountSpec(Type entityType) =>
        _countSpecs.TryGetValue(entityType, out var spec) ? spec : null;

    internal IReadOnlyList<object>? GetPrefill(Type entityType) =>
        _prefill.TryGetValue(entityType, out var list) ? list : null;

    internal int? SeedValue => _seed;

    internal IReadOnlyList<Action<IGeneratedData>> PostProcessSteps => _postProcessSteps;
}
