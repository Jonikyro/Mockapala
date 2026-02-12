namespace Mockapala.Schema;

/// <summary>
/// Immutable schema: entities in topological order and relations.
/// </summary>
public interface ISchema
{
    IReadOnlyList<IEntityDefinition> Entities { get; }
    IReadOnlyList<IRelationDefinition> Relations { get; }

    /// <summary>
    /// Entity types in generation order (dependencies first).
    /// </summary>
    IReadOnlyList<Type> GenerationOrder { get; }
}
