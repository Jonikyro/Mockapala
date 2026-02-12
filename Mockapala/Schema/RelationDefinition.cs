using Mockapala.Result;

namespace Mockapala.Schema;

/// <summary>
/// Type-safe relation definition: source entity has a foreign key to target entity's key.
/// Supports pair predicates (.Where), target-only predicates (.WhereTarget), unique assignment (.Unique),
/// optional FK (.Optional), and selector strategies.
/// </summary>
public sealed class RelationDefinition<TFrom, TTo> : IRelationDefinition
    where TFrom : class
    where TTo : class
{
    internal RelationDefinition(Action<object, object> setForeignKey)
    {
        SetForeignKey = setForeignKey;
    }

    public Type SourceType => typeof(TFrom);
    public Type TargetType => typeof(TTo);
    public Action<object, object> SetForeignKey { get; }
    public bool Required { get; private set; } = true;
    public Func<object, object, bool>? WherePredicate { get; private set; }
    public Func<object, object, IGeneratedData, bool>? WherePredicateWithData { get; private set; }
    public bool Unique { get; private set; }
    public SelectorStrategy Strategy { get; private set; } = SelectorStrategy.Random;
    public Func<object, double>? WeightFunction { get; private set; }

    /// <summary>
    /// Restricts which (source, target) pairs are eligible when resolving this relation.
    /// More expressive than WhereTarget: can use properties of both source and target.
    /// </summary>
    public RelationDefinition<TFrom, TTo> Where(Func<TFrom, TTo, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        WherePredicate = (src, tgt) => predicate((TFrom)src, (TTo)tgt);
        WherePredicateWithData = null;
        return this;
    }

    /// <summary>
    /// Restricts which (source, target) pairs are eligible, with access to all previously
    /// generated data. Use data.Get&lt;T&gt;() to look up entities earlier in the generation
    /// order and traverse indirect relations (e.g. filter by a grandparent property).
    /// </summary>
    public RelationDefinition<TFrom, TTo> Where(Func<TFrom, TTo, IGeneratedData, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        WherePredicateWithData = (src, tgt, data) => predicate((TFrom)src, (TTo)tgt, data);
        WherePredicate = null;
        return this;
    }

    /// <summary>
    /// Restricts which target entities are eligible when resolving this relation (target-only filter).
    /// Convenience for .Where((a, b) => predicate(b)).
    /// </summary>
    public RelationDefinition<TFrom, TTo> WhereTarget(Func<TTo, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        WherePredicate = (_, tgt) => predicate((TTo)tgt);
        WherePredicateWithData = null;
        return this;
    }

    /// <summary>
    /// Marks this relation as optional: when no eligible targets exist, set FK to null instead of throwing.
    /// </summary>
    public RelationDefinition<TFrom, TTo> Optional()
    {
        Required = false;
        return this;
    }

    /// <summary>
    /// When resolving this relation, each target is used at most once (one-to-one or unique FK).
    /// Requires at least as many eligible targets as source entities.
    /// </summary>
    public RelationDefinition<TFrom, TTo> IsUnique()
    {
        Unique = true;
        return this;
    }

    /// <summary>
    /// Sets the selector strategy for choosing target entities.
    /// </summary>
    public RelationDefinition<TFrom, TTo> WithStrategy(SelectorStrategy strategy)
    {
        Strategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets the selector strategy to Weighted with the given weight function.
    /// </summary>
    public RelationDefinition<TFrom, TTo> WithWeightedStrategy(Func<TTo, double> weightFunc)
    {
        if (weightFunc == null)
            throw new ArgumentNullException(nameof(weightFunc));
        Strategy = SelectorStrategy.Weighted;
        WeightFunction = obj => weightFunc((TTo)obj);
        return this;
    }
}
