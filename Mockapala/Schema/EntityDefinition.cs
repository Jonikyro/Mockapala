using System.Linq.Expressions;
using System.Reflection;

namespace Mockapala.Schema;

/// <summary>
/// Type-safe entity definition with key selector, optional generation rules, and relations.
/// Relations are declared inside the entity callback (Mockapala style).
/// </summary>
public sealed class EntityDefinition<T> : IEntityDefinition where T : class
{
    private Func<object, object>? _getKey;
    private Action<object, object>? _setKey;
    private Type? _keyType;
    private Func<int, object>? _customKeyGenerator;
    private readonly List<IRelationDefinition> _relations = new();

    internal Action<Bogus.Faker<T>>? FakerRules { get; private set; }

    public Type EntityType => typeof(T);
    public Type KeyType => _keyType ?? throw new InvalidOperationException("Key has not been set. Call Key(...) when defining the entity.");

    public Func<object, object> GetKey =>
        _getKey ?? throw new InvalidOperationException("Key has not been set. Call Key(...) when defining the entity.");

    public Action<object, object> SetKey =>
        _setKey ?? throw new InvalidOperationException("Key has not been set. Call Key(...) when defining the entity.");

    public Func<int, object>? CustomKeyGenerator => _customKeyGenerator;

    public IReadOnlyList<IRelationDefinition> Relations => _relations;

    /// <summary>
    /// Sets the key property using an expression (e.g. c => c.Id).
    /// The key is used for identity and for resolving foreign keys.
    /// </summary>
    public EntityDefinition<T> Key<TKey>(Expression<Func<T, TKey>> keySelector)
    {
        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));

        _keyType = typeof(TKey);
        var getter = keySelector.Compile();
        _getKey = obj =>
        {
            var value = getter((T)obj);
            return value!;
        };

        if (keySelector.Body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert
            && unary.Operand is MemberExpression innerMember)
        {
            BuildSetter(innerMember.Member);
        }
        else if (keySelector.Body is MemberExpression memberExpr)
        {
            BuildSetter(memberExpr.Member);
        }

        if (_setKey == null)
            throw new ArgumentException("Key must be a writable property or field.", nameof(keySelector));

        return this;
    }

    private void BuildSetter(MemberInfo member)
    {
        if (member is PropertyInfo prop && prop.CanWrite)
            _setKey = (obj, value) => prop.SetValue(obj, value);
        else if (member is FieldInfo field)
            _setKey = (obj, value) => field.SetValue(obj, value);
    }

    /// <summary>
    /// Sets a custom key generator that produces the key value from the 1-based sequential index.
    /// </summary>
    public EntityDefinition<T> KeyGenerator<TKey>(Func<int, TKey> generator) where TKey : notnull
    {
        _ = KeyType; // ensure Key() was called
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        _customKeyGenerator = i => generator(i);
        return this;
    }

    /// <summary>
    /// Sets a custom key generator with a raw-to-key conversion (for strongly-typed IDs).
    /// </summary>
    public EntityDefinition<T> KeyGenerator<TRaw, TKey>(Func<int, TRaw> generator, Func<TRaw, TKey> conversion)
        where TKey : notnull
    {
        _ = KeyType;
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        if (conversion == null)
            throw new ArgumentNullException(nameof(conversion));
        _customKeyGenerator = i => conversion(generator(i))!;
        return this;
    }

    /// <summary>
    /// Configures Bogus Faker rules for this entity.
    /// </summary>
    public EntityDefinition<T> WithRules(Action<Bogus.Faker<T>> rules)
    {
        if (rules == null)
            throw new ArgumentNullException(nameof(rules));
        FakerRules = rules;
        return this;
    }

    /// <summary>
    /// Declares a relation from this entity to a target entity, using the FK property on the source.
    /// Target key is inferred from the target entity's key definition at Build() time.
    /// Returns the relation for chaining (.Where, .Optional, .IsUnique, .WithStrategy).
    /// </summary>
    public RelationDefinition<T, TTarget> Relation<TTarget>(Expression<Func<T, object?>> foreignKeySelector)
        where TTarget : class
    {
        if (foreignKeySelector == null)
            throw new ArgumentNullException(nameof(foreignKeySelector));

        var fkMember = GetMemberFromExpression(foreignKeySelector);
        if (fkMember == null)
            throw new ArgumentException("Expression must be simple member access (e.g. o => o.CustomerId).", nameof(foreignKeySelector));

        Action<object, object> setFk = (source, targetKeyValue) =>
        {
            if (fkMember is PropertyInfo prop)
                prop.SetValue(source, targetKeyValue);
            else if (fkMember is FieldInfo field)
                field.SetValue(source, targetKeyValue);
        };

        var relation = new RelationDefinition<T, TTarget>(setFk);
        _relations.Add(relation);
        return relation;
    }

    /// <summary>
    /// Declares a relation with an explicit target key selector (for non-default target keys).
    /// </summary>
    public RelationDefinition<T, TTarget> Relation<TTarget>(
        Expression<Func<T, object?>> foreignKeySelector,
        Expression<Func<TTarget, object?>> targetKeySelector)
        where TTarget : class
    {
        // Target key selector is accepted for documentation/validation but not stored:
        // at resolution time the generator uses targetDef.GetKey(target).
        if (targetKeySelector == null)
            throw new ArgumentNullException(nameof(targetKeySelector));
        return Relation<TTarget>(foreignKeySelector);
    }

    private static MemberInfo? GetMemberFromExpression(LambdaExpression expr)
    {
        var body = expr.Body;
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            body = unary.Operand;
        return body is MemberExpression member ? member.Member : null;
    }
}
