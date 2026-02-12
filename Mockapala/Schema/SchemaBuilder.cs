namespace Mockapala.Schema;

/// <summary>
/// Fluent builder for a Mockapala schema. Entities (with keys, rules, and relations) are defined
/// in the entity callback. Relations are colocated with the entity that owns the FK.
/// </summary>
public sealed class SchemaBuilder
{
    private readonly List<IEntityDefinition> _entities = new();
    private readonly Dictionary<Type, IEntityDefinition> _entityByType = new();

    internal SchemaBuilder() { }

    /// <summary>
    /// Registers an entity type. Use the configure callback to set Key, WithRules, and Relation.
    /// Returns the builder for chaining.
    /// </summary>
    public SchemaBuilder Entity<T>(Action<EntityDefinition<T>>? configure = null) where T : class
    {
        if (_entityByType.ContainsKey(typeof(T)))
            throw new InvalidOperationException($"Entity type {typeof(T).Name} is already registered.");

        var definition = new EntityDefinition<T>();
        configure?.Invoke(definition);
        _entities.Add(definition);
        _entityByType[typeof(T)] = definition;
        return this;
    }

    /// <summary>
    /// Builds the schema. Validates keys, collects relations, and computes topological generation order.
    /// Self-referential relations are allowed; cross-type cycles throw.
    /// </summary>
    public ISchema Build()
    {
        // Validate all entities have keys
        foreach (var e in _entities)
        {
            _ = e.GetKey;
            _ = e.SetKey;
        }

        // Collect all relations from all entities
        var allRelations = new List<IRelationDefinition>();
        foreach (var e in _entities)
            allRelations.AddRange(e.Relations);

        // Validate: every relation's target type must be a registered entity
        foreach (var rel in allRelations)
        {
            if (!_entityByType.ContainsKey(rel.TargetType))
                throw new InvalidOperationException(
                    $"Relation {rel.SourceType.Name} -> {rel.TargetType.Name}: target type {rel.TargetType.Name} is not registered as an entity.");
        }

        var entityTypes = _entities.Select(x => x.EntityType).Distinct().ToList();
        var order = DependencyGraph.TopologicalSort(entityTypes, allRelations);
        return new Schema(_entities, allRelations, order);
    }
}
