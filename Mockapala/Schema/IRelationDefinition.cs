using Mockapala.Result;

namespace Mockapala.Schema;

/// <summary>
/// Non-generic view of a relation (source entity has FK pointing to target entity's key).
/// </summary>
public interface IRelationDefinition
{
    Type SourceType { get; }
    Type TargetType { get; }
    Action<object, object> SetForeignKey { get; }

    /// <summary>
    /// When true, the relation is required (no eligible target → throw). When false, set FK to null.
    /// Default: true.
    /// </summary>
    bool Required { get; }

    /// <summary>
    /// Pair-based predicate: (source, target) → eligible. Null means all targets are eligible.
    /// Set by .Where((a,b)=>...) or .WhereTarget(b=>...).
    /// </summary>
    Func<object, object, bool>? WherePredicate { get; }

    /// <summary>
    /// Pair-based predicate with access to all previously generated data:
    /// (source, target, data) → eligible. Null means not set.
    /// Set by .Where((a, b, data) => ...). Mutually exclusive with WherePredicate.
    /// </summary>
    Func<object, object, IGeneratedData, bool>? WherePredicateWithData { get; }

    /// <summary>
    /// When true, each target is used at most once when resolving FKs (assign without replacement).
    /// </summary>
    bool Unique { get; }

    /// <summary>
    /// Strategy for choosing target entities when resolving the FK.
    /// </summary>
    SelectorStrategy Strategy { get; }

    /// <summary>
    /// Weight function for Weighted selector strategy. Only used when Strategy == Weighted.
    /// </summary>
    Func<object, double>? WeightFunction { get; }
}
