using System.Reflection;

namespace Mockapala.Schema;

/// <summary>
/// Fluent builder for configuring a property on an entity definition.
/// Returned by <see cref="EntityDefinition{T}.Property{TProp}"/>.
/// </summary>
public sealed class PropertyBuilder<T, TProp> where T : class
{
    private readonly EntityDefinition<T> _entity;
    private readonly string _propertyName;
    private readonly MemberInfo _member;

    internal PropertyBuilder(EntityDefinition<T> entity, string propertyName, MemberInfo member)
    {
        _entity = entity;
        _propertyName = propertyName;
        _member = member;
    }

    /// <summary>
    /// Registers a one-way conversion for this property, used at export time.
    /// </summary>
    public PropertyBuilder<T, TProp> HasConversion<TConverted>(Func<TProp, TConverted> converter)
    {
        if (converter == null)
            throw new ArgumentNullException(nameof(converter));

        var conversion = new PropertyConversion(
            _propertyName,
            _member,
            typeof(TConverted),
            obj => converter((TProp)obj)!);

        _entity.AddConversion(conversion);
        return this;
    }
}
