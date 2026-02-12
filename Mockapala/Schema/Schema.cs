namespace Mockapala.Schema;

internal sealed class Schema : ISchema
{
    public Schema(
        IReadOnlyList<IEntityDefinition> entities,
        IReadOnlyList<IRelationDefinition> relations,
        IReadOnlyList<Type> generationOrder)
    {
        Entities = entities;
        Relations = relations;
        GenerationOrder = generationOrder;
    }

    public IReadOnlyList<IEntityDefinition> Entities { get; }
    public IReadOnlyList<IRelationDefinition> Relations { get; }
    public IReadOnlyList<Type> GenerationOrder { get; }
}
