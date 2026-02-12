namespace Mockapala.Schema;

/// <summary>
/// Builds a DAG from relations and provides topological order.
/// Self-referential edges (SourceType == TargetType) are excluded from cycle detection.
/// </summary>
public static class DependencyGraph
{
    /// <summary>
    /// Returns entity types in generation order (dependencies first).
    /// Self-edges are ignored for cycle detection; cross-type cycles throw <see cref="CircularDependencyException"/>.
    /// </summary>
    public static IReadOnlyList<Type> TopologicalSort(
        IReadOnlyList<Type> entityTypes,
        IReadOnlyList<IRelationDefinition> relations)
    {
        var typeToIndex = new Dictionary<Type, int>();
        for (var i = 0; i < entityTypes.Count; i++)
            typeToIndex[entityTypes[i]] = i;

        var n = entityTypes.Count;
        var adjacency = new List<int>[n];
        for (var i = 0; i < n; i++)
            adjacency[i] = new List<int>();

        foreach (var rel in relations)
        {
            // Skip self-referential edges — they do not create cross-type cycles
            if (rel.SourceType == rel.TargetType)
                continue;

            if (!typeToIndex.TryGetValue(rel.SourceType, out var fromIdx) ||
                !typeToIndex.TryGetValue(rel.TargetType, out var toIdx))
                continue;

            // Source depends on Target: generate Target before Source
            adjacency[toIdx].Add(fromIdx);
        }

        // Detect cycle
        var cycle = FindCycle(n, adjacency, entityTypes);
        if (cycle != null)
            throw new CircularDependencyException(cycle);

        // Kahn's algorithm
        var inDegree = new int[n];
        foreach (var list in adjacency)
            foreach (var v in list)
                inDegree[v]++;

        var queue = new Queue<int>();
        for (var i = 0; i < n; i++)
            if (inDegree[i] == 0)
                queue.Enqueue(i);

        var order = new List<Type>();
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            order.Add(entityTypes[u]);
            foreach (var v in adjacency[u])
            {
                inDegree[v]--;
                if (inDegree[v] == 0)
                    queue.Enqueue(v);
            }
        }

        if (order.Count != n)
            throw new CircularDependencyException(FindCycle(n, adjacency, entityTypes) ?? new List<string> { "Unknown cycle" });

        return order;
    }

    private static IReadOnlyList<string>? FindCycle(int n, List<int>[] adjacency, IReadOnlyList<Type> entityTypes)
    {
        var color = new int[n]; // 0 white, 1 gray, 2 black
        var parent = new int[n];
        for (var i = 0; i < n; i++)
            parent[i] = -1;

        int? cycleStart = null;
        int? cycleEnd = null;

        void Dfs(int v)
        {
            color[v] = 1;
            foreach (var w in adjacency[v])
            {
                if (color[w] == 1)
                {
                    cycleStart = w;
                    cycleEnd = v;
                    return;
                }
                if (color[w] == 0)
                {
                    parent[w] = v;
                    Dfs(w);
                    if (cycleStart.HasValue)
                        return;
                }
            }
            color[v] = 2;
        }

        for (var i = 0; i < n && !cycleStart.HasValue; i++)
        {
            if (color[i] == 0)
                Dfs(i);
        }

        if (!cycleStart.HasValue || !cycleEnd.HasValue)
            return null;

        var path = new List<int>();
        var cur = cycleEnd.Value;
        while (true)
        {
            path.Add(cur);
            if (cur == cycleStart.Value)
                break;
            cur = parent[cur];
        }
        path.Reverse();
        return path.Select(i => entityTypes[i].Name).ToList();
    }
}
