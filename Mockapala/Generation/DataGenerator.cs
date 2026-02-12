using System.Reflection;
using Mockapala.Result;
using Mockapala.Schema;

namespace Mockapala.Generation;

/// <summary>
/// Generates test data from a schema in dependency order, resolving FKs and applying business rules.
/// Supports optional relations, pair predicates, selector strategies, self-reference, prefill, and post-processing.
/// </summary>
public sealed class DataGenerator
{
    /// <summary>
    /// Generates data in-memory according to the schema and config.
    /// </summary>
    public IGeneratedData Generate(ISchema schema, Action<GenerationConfig>? configure = null)
    {
        var config = new GenerationConfig();
        configure?.Invoke(config);
        var seed = config.SeedValue;

        var result = new GeneratedData();
        var generatedByType = new Dictionary<Type, IReadOnlyList<object>>();

        foreach (var entityType in schema.GenerationOrder)
        {
            var definition = schema.Entities.First(e => e.EntityType == entityType);
            var relationsForThis = schema.Relations.Where(r => r.SourceType == entityType).ToList();

            var prefill = config.GetPrefill(entityType);
            var countSpec = config.GetCountSpec(entityType);
            var flexible = countSpec?.Flexible ?? false;
            IReadOnlyList<object> list;

            if (prefill != null)
            {
                // Prefill path: use supplied instances, resolve relations only
                list = prefill;
            }
            else
            {
                var count = countSpec?.Count ?? 0;
                if (count <= 0)
                    continue;

                list = GenerateEntities(definition, count, seed);
            }

            // Resolve relations (for both generated and prefilled entities)
            // When flexible, failed entities are collected in discardSet instead of throwing
            HashSet<int>? discardSet = flexible ? new HashSet<int>() : null;
            ResolveRelations(list, relationsForThis, generatedByType, schema, seed, result, discardSet);

            // Remove discarded entities (only when using IdealCount)
            if (discardSet != null && discardSet.Count > 0)
            {
                var survivors = new List<object>(list.Count - discardSet.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    if (!discardSet.Contains(i))
                        survivors.Add(list[i]);
                }

                var min = countSpec!.Min;
                if (survivors.Count < min)
                    throw new InvalidOperationException(
                        $"Generated {list.Count} {entityType.Name}(s) but only {survivors.Count} survived relation resolution " +
                        $"({discardSet.Count} discarded due to no eligible targets). Minimum required: {min}. " +
                        $"Increase the target entity counts or relax the relation predicates.");

                list = survivors;
            }

            generatedByType[entityType] = list;
            result.Set(entityType, list);
        }

        // Run post-processing steps
        foreach (var step in config.PostProcessSteps)
            step(result);

        return result;
    }

    private static IReadOnlyList<object> GenerateEntities(
        IEntityDefinition definition,
        int count,
        int? seed)
    {
        var entityType = definition.EntityType;
        var method = typeof(DataGenerator)
            .GetMethod(nameof(GenerateTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);
        try
        {
            return (IReadOnlyList<object>)method.Invoke(null, new object?[] { definition, count, seed })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static IReadOnlyList<object> GenerateTyped<T>(
        IEntityDefinition definition,
        int count,
        int? seed) where T : class
    {
        var def = (EntityDefinition<T>)definition;
        var faker = new Bogus.Faker<T>();
        if (seed.HasValue)
            faker.UseSeed(seed.Value);

        def.FakerRules?.Invoke(faker);

        var list = new List<object>(count);
        var keyType = definition.KeyType;
        for (var i = 0; i < count; i++)
        {
            var entity = faker.Generate();
            var key = definition.CustomKeyGenerator != null
                ? definition.CustomKeyGenerator(i + 1)
                : CreateKey(keyType, i + 1);
            definition.SetKey(entity!, key);
            list.Add(entity!);
        }

        return list;
    }

    private static void ResolveRelations(
        IReadOnlyList<object> currentBatch,
        IReadOnlyList<IRelationDefinition> relations,
        Dictionary<Type, IReadOnlyList<object>> generatedByType,
        ISchema schema,
        int? seed,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        foreach (var rel in relations)
        {
            // For self-referential relations, use current batch as targets
            IReadOnlyList<object> targetList;
            if (rel.SourceType == rel.TargetType)
            {
                targetList = currentBatch;
            }
            else
            {
                targetList = generatedByType.TryGetValue(rel.TargetType, out var targets)
                    ? targets
                    : (IReadOnlyList<object>)Array.Empty<object>();
            }

            var targetDef = schema.Entities.First(e => e.EntityType == rel.TargetType);
            var random = seed.HasValue
                ? new Random(seed.Value + rel.SourceType.GetHashCode() + rel.TargetType.GetHashCode())
                : new Random();

            switch (rel.Strategy)
            {
                case SelectorStrategy.Random:
                    ResolveRandom(currentBatch, rel, targetList, targetDef, random, data, discardSet);
                    break;
                case SelectorStrategy.RoundRobin:
                    ResolveRoundRobin(currentBatch, rel, targetList, targetDef, random, data, discardSet);
                    break;
                case SelectorStrategy.SpreadEvenly:
                    ResolveSpreadEvenly(currentBatch, rel, targetList, targetDef, random, data, discardSet);
                    break;
                case SelectorStrategy.Weighted:
                    ResolveWeighted(currentBatch, rel, targetList, targetDef, random, data, discardSet);
                    break;
                default:
                    throw new NotSupportedException($"Selector strategy {rel.Strategy} is not supported.");
            }
        }
    }

    private static void ResolveRandom(
        IReadOnlyList<object> sources,
        IRelationDefinition rel,
        IReadOnlyList<object> targetList,
        IEntityDefinition targetDef,
        Random random,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        if (rel.Unique)
        {
            ResolveUnique(sources, rel, targetList, targetDef, random, data, discardSet);
            return;
        }

        for (var i = 0; i < sources.Count; i++)
        {
            if (discardSet != null && discardSet.Contains(i)) continue;

            var source = sources[i];
            var filtered = FilterTargets(source, rel, targetList, data);

            if (filtered.Count == 0)
            {
                if (!rel.Required)
                {
                    SetFkNull(source, rel);
                    continue;
                }
                if (discardSet != null) { discardSet.Add(i); continue; }
                ThrowNoTargets(rel, targetList);
            }

            var target = filtered[random.Next(filtered.Count)];
            var targetKey = targetDef.GetKey(target);
            rel.SetForeignKey(source, targetKey);
        }
    }

    private static void ResolveRoundRobin(
        IReadOnlyList<object> sources,
        IRelationDefinition rel,
        IReadOnlyList<object> targetList,
        IEntityDefinition targetDef,
        Random random,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        if (rel.Unique)
        {
            ResolveUnique(sources, rel, targetList, targetDef, random, data, discardSet);
            return;
        }

        // For round-robin with pair predicate, we need per-source filtering
        // If no predicate, we can use a simple index
        if (rel.WherePredicate == null && rel.WherePredicateWithData == null)
        {
            if (targetList.Count == 0)
            {
                HandleEmptyTargets(sources, rel, targetList, discardSet);
                return;
            }

            for (var i = 0; i < sources.Count; i++)
            {
                if (discardSet != null && discardSet.Contains(i)) continue;
                var target = targetList[i % targetList.Count];
                var targetKey = targetDef.GetKey(target);
                rel.SetForeignKey(sources[i], targetKey);
            }
        }
        else
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (discardSet != null && discardSet.Contains(i)) continue;
                var source = sources[i];
                var filtered = FilterTargets(source, rel, targetList, data);
                if (filtered.Count == 0)
                {
                    if (!rel.Required) { SetFkNull(source, rel); continue; }
                    if (discardSet != null) { discardSet.Add(i); continue; }
                    ThrowNoTargets(rel, targetList);
                }
                var target = filtered[i % filtered.Count];
                var targetKey = targetDef.GetKey(target);
                rel.SetForeignKey(source, targetKey);
            }
        }
    }

    private static void ResolveSpreadEvenly(
        IReadOnlyList<object> sources,
        IRelationDefinition rel,
        IReadOnlyList<object> targetList,
        IEntityDefinition targetDef,
        Random random,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        if (rel.Unique)
        {
            ResolveUnique(sources, rel, targetList, targetDef, random, data, discardSet);
            return;
        }

        if (rel.WherePredicate == null && rel.WherePredicateWithData == null)
        {
            if (targetList.Count == 0)
            {
                HandleEmptyTargets(sources, rel, targetList, discardSet);
                return;
            }

            // Assign sources[i] to target[i % targetCount] — same as round-robin for spread-evenly
            for (var i = 0; i < sources.Count; i++)
            {
                if (discardSet != null && discardSet.Contains(i)) continue;
                var target = targetList[i % targetList.Count];
                var targetKey = targetDef.GetKey(target);
                rel.SetForeignKey(sources[i], targetKey);
            }
        }
        else
        {
            for (var i = 0; i < sources.Count; i++)
            {
                if (discardSet != null && discardSet.Contains(i)) continue;
                var source = sources[i];
                var filtered = FilterTargets(source, rel, targetList, data);
                if (filtered.Count == 0)
                {
                    if (!rel.Required) { SetFkNull(source, rel); continue; }
                    if (discardSet != null) { discardSet.Add(i); continue; }
                    ThrowNoTargets(rel, targetList);
                }
                var target = filtered[i % filtered.Count];
                var targetKey = targetDef.GetKey(target);
                rel.SetForeignKey(source, targetKey);
            }
        }
    }

    private static void ResolveWeighted(
        IReadOnlyList<object> sources,
        IRelationDefinition rel,
        IReadOnlyList<object> targetList,
        IEntityDefinition targetDef,
        Random random,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        if (rel.WeightFunction == null)
            throw new InvalidOperationException(
                $"Relation {rel.SourceType.Name} -> {rel.TargetType.Name} uses Weighted strategy but no weight function was set. Use WithWeightedStrategy(func).");

        for (var i = 0; i < sources.Count; i++)
        {
            if (discardSet != null && discardSet.Contains(i)) continue;

            var source = sources[i];
            var filtered = FilterTargets(source, rel, targetList, data);

            if (filtered.Count == 0)
            {
                if (!rel.Required) { SetFkNull(source, rel); continue; }
                if (discardSet != null) { discardSet.Add(i); continue; }
                ThrowNoTargets(rel, targetList);
            }

            var target = PickWeighted(filtered, rel.WeightFunction, random);
            var targetKey = targetDef.GetKey(target);
            rel.SetForeignKey(source, targetKey);
        }
    }

    private static object PickWeighted(List<object> filtered, Func<object, double> weightFunc, Random random)
    {
        var weights = new double[filtered.Count];
        var totalWeight = 0.0;
        for (var i = 0; i < filtered.Count; i++)
        {
            weights[i] = Math.Max(0, weightFunc(filtered[i]));
            totalWeight += weights[i];
        }

        if (totalWeight <= 0)
            return filtered[random.Next(filtered.Count)]; // fallback to uniform

        var r = random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        for (var i = 0; i < filtered.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative)
                return filtered[i];
        }

        return filtered[^1]; // fallback
    }

    private static void ResolveUnique(
        IReadOnlyList<object> sources,
        IRelationDefinition rel,
        IReadOnlyList<object> targetList,
        IEntityDefinition targetDef,
        Random random,
        IGeneratedData data,
        HashSet<int>? discardSet)
    {
        // For unique with pair predicate, we need to handle per-source filtering
        // Simple approach: collect all eligible targets, ensure enough, shuffle & assign
        if (rel.WherePredicate == null && rel.WherePredicateWithData == null)
        {
            if (targetList.Count == 0 && sources.Count > 0)
            {
                if (!rel.Required)
                {
                    foreach (var source in sources) SetFkNull(source, rel);
                    return;
                }
                if (discardSet != null)
                {
                    for (var i = 0; i < sources.Count; i++) discardSet.Add(i);
                    return;
                }
                ThrowNoTargets(rel, targetList);
            }

            if (targetList.Count < sources.Count)
            {
                if (!rel.Required)
                {
                    // Assign what we can, rest get null
                    var perm = ShuffleIndices(targetList.Count, random);
                    for (var i = 0; i < sources.Count; i++)
                    {
                        if (i < targetList.Count)
                        {
                            var target = targetList[perm[i]];
                            rel.SetForeignKey(sources[i], targetDef.GetKey(target));
                        }
                        else
                        {
                            SetFkNull(sources[i], rel);
                        }
                    }
                    return;
                }
                if (discardSet != null)
                {
                    // Assign what we can, discard the rest
                    var perm = ShuffleIndices(targetList.Count, random);
                    for (var i = 0; i < sources.Count; i++)
                    {
                        if (i < targetList.Count)
                        {
                            var target = targetList[perm[i]];
                            rel.SetForeignKey(sources[i], targetDef.GetKey(target));
                        }
                        else
                        {
                            discardSet.Add(i);
                        }
                    }
                    return;
                }
                throw new InvalidOperationException(
                    $"Relation {rel.SourceType.Name} -> {rel.TargetType.Name} is Unique but there are {sources.Count} source(s) and only {targetList.Count} eligible target(s). Generate at least {sources.Count} targets (or make the relation Optional).");
            }

            var permutation = ShuffleIndices(targetList.Count, random);
            for (var i = 0; i < sources.Count; i++)
            {
                var target = targetList[permutation[i]];
                var targetKey = targetDef.GetKey(target);
                rel.SetForeignKey(sources[i], targetKey);
            }
        }
        else
        {
            // With pair predicate: for each source, compute eligible set, pick unique
            // This is more complex — we use a greedy approach
            var usedTargets = new HashSet<int>();
            for (var i = 0; i < sources.Count; i++)
            {
                if (discardSet != null && discardSet.Contains(i)) continue;

                var source = sources[i];
                var filtered = FilterTargets(source, rel, targetList, data);

                // Remove already-used targets
                var available = new List<(object target, int originalIndex)>();
                for (var j = 0; j < filtered.Count; j++)
                {
                    // Find the index in the original target list
                    var idx = -1;
                    for (var k = 0; k < targetList.Count; k++)
                    {
                        if (ReferenceEquals(targetList[k], filtered[j]))
                        {
                            idx = k;
                            break;
                        }
                    }
                    if (idx >= 0 && !usedTargets.Contains(idx))
                        available.Add((filtered[j], idx));
                }

                if (available.Count == 0)
                {
                    if (!rel.Required) { SetFkNull(source, rel); continue; }
                    if (discardSet != null) { discardSet.Add(i); continue; }
                    throw new InvalidOperationException(
                        $"Relation {rel.SourceType.Name} -> {rel.TargetType.Name} is Unique but source at index {i} has no available eligible targets.");
                }

                var pick = available[random.Next(available.Count)];
                usedTargets.Add(pick.originalIndex);
                var targetKey = targetDef.GetKey(pick.target);
                rel.SetForeignKey(source, targetKey);
            }
        }
    }

    private static List<object> FilterTargets(object source, IRelationDefinition rel, IReadOnlyList<object> targetList, IGeneratedData data)
    {
        if (rel.WherePredicateWithData != null)
            return targetList.Where(t => rel.WherePredicateWithData(source, t, data)).ToList();
        if (rel.WherePredicate != null)
            return targetList.Where(t => rel.WherePredicate(source, t)).ToList();
        return targetList.ToList();
    }

    private static void HandleEmptyTargets(IReadOnlyList<object> sources, IRelationDefinition rel, IReadOnlyList<object> targetList, HashSet<int>? discardSet)
    {
        if (sources.Count > 0)
        {
            if (!rel.Required)
            {
                foreach (var source in sources) SetFkNull(source, rel);
                return;
            }
            if (discardSet != null)
            {
                for (var i = 0; i < sources.Count; i++) discardSet.Add(i);
                return;
            }
            ThrowNoTargets(rel, targetList);
        }
    }

    private static void SetFkNull(object source, IRelationDefinition rel)
    {
        // Set FK to null (for nullable FK properties)
        try
        {
            rel.SetForeignKey(source, null!);
        }
        catch
        {
            // If the FK property is not nullable, silently skip (value type)
        }
    }

    private static void ThrowNoTargets(IRelationDefinition rel, IReadOnlyList<object> targetList)
    {
        if (targetList.Count == 0)
            throw new InvalidOperationException(
                $"Relation {rel.SourceType.Name} -> {rel.TargetType.Name}: cannot resolve FK because no {rel.TargetType.Name} entities were generated. Generate at least one {rel.TargetType.Name} or make the relation Optional.");
        throw new InvalidOperationException(
            $"Where predicate for relation {rel.SourceType.Name} -> {rel.TargetType.Name} excluded all {targetList.Count} target(s). Ensure at least one target satisfies the filter or make the relation Optional.");
    }

    private static int[] ShuffleIndices(int count, Random random)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++)
            indices[i] = i;
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }

    private static object CreateKey(Type keyType, int sequential)
    {
        if (keyType == typeof(int))
            return sequential;
        if (keyType == typeof(long))
            return (long)sequential;
        if (keyType == typeof(Guid))
            return Guid.NewGuid();
        if (keyType == typeof(string))
            return sequential.ToString();
        throw new NotSupportedException(
            $"Key type {keyType.Name} is not supported. Set a KeyGenerator for this entity, or use a key type of int, long, Guid, or string.");
    }
}
