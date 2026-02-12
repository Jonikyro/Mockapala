namespace Mockapala.Schema;

/// <summary>
/// Thrown when the relation graph contains a cycle (excluding self-referential edges).
/// </summary>
public sealed class CircularDependencyException : Exception
{
    public IReadOnlyList<string> Cycle { get; }

    public CircularDependencyException(IReadOnlyList<string> cycle)
        : base($"Circular dependency detected: {string.Join(" → ", cycle)} → {cycle[0]}")
    {
        Cycle = cycle;
    }
}
