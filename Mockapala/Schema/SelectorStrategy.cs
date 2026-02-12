namespace Mockapala.Schema;

/// <summary>
/// Strategy for choosing target entities when resolving a relation's foreign key.
/// </summary>
public enum SelectorStrategy
{
    /// <summary>Pick a random eligible target (with replacement). Default.</summary>
    Random = 0,

    /// <summary>Cycle through eligible targets in order: source[i] gets target[i % count].</summary>
    RoundRobin,

    /// <summary>Distribute sources as evenly as possible across eligible targets.</summary>
    SpreadEvenly,

    /// <summary>Pick targets using a weight function (weighted random). Requires WeightFunction on the relation.</summary>
    Weighted
}
