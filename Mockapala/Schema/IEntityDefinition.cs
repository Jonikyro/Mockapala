namespace Mockapala.Schema;

/// <summary>
/// Non-generic view of an entity definition (type, key, and generation rules).
/// </summary>
public interface IEntityDefinition
{
    Type EntityType { get; }
    Type KeyType { get; }
    Func<object, object> GetKey { get; }
    Action<object, object> SetKey { get; }

    /// <summary>
    /// When set, used to generate the key value from the 1-based sequential index instead of inferring from key type.
    /// </summary>
    Func<int, object>? CustomKeyGenerator { get; }

    /// <summary>
    /// Returns the relations declared on this entity.
    /// </summary>
    IReadOnlyList<IRelationDefinition> Relations { get; }

    /// <summary>
    /// Returns the property conversions declared on this entity.
    /// </summary>
    IReadOnlyList<PropertyConversion> Conversions { get; }
}
