using System.Reflection;
using Mockapala.Schema;

namespace Mockapala.Export;

/// <summary>
/// Describes a property to export: the source property, the effective type after conversion,
/// and an optional converter function.
/// </summary>
public sealed class ExportableProperty
{
    public ExportableProperty(PropertyInfo property, Type effectiveType, Func<object?, object?>? converter)
    {
        Property = property;
        EffectiveType = effectiveType;
        Converter = converter;
    }

    /// <summary>The source property on the entity.</summary>
    public PropertyInfo Property { get; }

    /// <summary>The type to use for column/parameter inference (converted type if a conversion exists, otherwise the property type).</summary>
    public Type EffectiveType { get; }

    /// <summary>Optional converter. When null, the raw property value is used.</summary>
    public Func<object?, object?>? Converter { get; }

    /// <summary>Reads the property value from the entity, applying the conversion if present.</summary>
    public object? GetValue(object entity)
    {
        var raw = Property.GetValue(entity);
        return Converter != null ? Converter(raw) : raw;
    }

    /// <summary>
    /// Builds the list of exportable properties for an entity type, incorporating conversions from the definition.
    /// </summary>
    public static IReadOnlyList<ExportableProperty> GetExportableProperties(Type entityType, IEntityDefinition? definition)
    {
        var conversions = definition?.Conversions;
        var conversionByName = new Dictionary<string, PropertyConversion>();
        if (conversions != null)
        {
            foreach (var c in conversions)
                conversionByName[c.PropertyName] = c;
        }

        var result = new List<ExportableProperty>();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            if (conversionByName.TryGetValue(prop.Name, out var conversion))
            {
                // Property has a conversion — always exportable, use converted type
                result.Add(new ExportableProperty(prop, conversion.ConvertedType, raw => raw != null ? conversion.Converter(raw) : null));
            }
            else if (IsScalarType(prop.PropertyType))
            {
                // Scalar property without conversion — exportable as-is
                result.Add(new ExportableProperty(prop, prop.PropertyType, null));
            }
            // Non-scalar without conversion — skip
        }

        return result;
    }

    private static bool IsScalarType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive
            || t.IsEnum
            || t == typeof(string)
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(TimeSpan)
            || t == typeof(Guid)
            || t == typeof(byte[]);
    }
}
