namespace Mockapala.Schema;

/// <summary>
/// Fluent builder for configuring the key of an entity definition.
/// Returned by <see cref="EntityDefinition{T}.Key{TKey}"/>. Provides compile-time
/// type-safe key generator registration via <see cref="WithGenerator"/>.
/// </summary>
public sealed class KeyBuilder<T, TKey> where T : class where TKey : notnull
{
    private readonly EntityDefinition<T> _entity;

    internal KeyBuilder(EntityDefinition<T> entity)
    {
        _entity = entity;
    }

    /// <summary>
    /// Sets a custom key generator that produces the key value from the 1-based sequential index.
    /// The generator must return <typeparamref name="TKey"/> matching the key property type.
    /// </summary>
    public KeyBuilder<T, TKey> WithGenerator(Func<int, TKey> generator)
    {
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        _entity.SetCustomKeyGenerator(i => generator(i));
        return this;
    }

    /// <summary>
    /// Sets a custom key generator with a raw-to-key conversion (for strongly-typed IDs).
    /// </summary>
    public KeyBuilder<T, TKey> WithGenerator<TRaw>(Func<int, TRaw> generator, Func<TRaw, TKey> conversion)
    {
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        if (conversion == null)
            throw new ArgumentNullException(nameof(conversion));
        _entity.SetCustomKeyGenerator(i => conversion(generator(i))!);
        return this;
    }
}
