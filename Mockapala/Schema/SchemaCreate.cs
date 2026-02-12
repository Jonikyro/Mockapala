namespace Mockapala.Schema;

/// <summary>
/// Entry point for building a Mockapala schema.
/// </summary>
public static class SchemaCreate
{
    /// <summary>
    /// Creates a new schema builder. Define entities with Key, WithRules, and Relation inside the entity callback, then Build().
    /// </summary>
    public static SchemaBuilder Create() => new SchemaBuilder();
}
