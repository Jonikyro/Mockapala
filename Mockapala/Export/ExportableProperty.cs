using System.Reflection;
using Mockapala.Schema;

namespace Mockapala.Export;

/// <summary>
/// Describes a property to export: the source property, the effective type after conversion,
/// and an optional converter function.
/// </summary>
public sealed class ExportableProperty
{
    public ExportableProperty(PropertyInfo property, Type effectiveType, Func<object?, object?>? converter, string? columnName = null)
    {
        Property = property;
        EffectiveType = effectiveType;
        Converter = converter;
        ColumnName = columnName ?? property.Name;
    }

    /// <summary>The source property on the entity.</summary>
    public PropertyInfo Property { get; }

    /// <summary>The column name to use when exporting. Defaults to the property name.</summary>
    public string ColumnName { get; }

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

        var columnNames = definition?.ColumnNames;

        var result = new List<ExportableProperty>();
        var properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            string? colName = null;
            columnNames?.TryGetValue(prop.Name, out colName);

            if (conversionByName.TryGetValue(prop.Name, out var conversion))
            {
                result.Add(new ExportableProperty(prop, conversion.ConvertedType, raw => raw != null ? conversion.Converter(raw) : null, colName));
            }
            else if (IsScalarType(prop.PropertyType))
            {
                result.Add(new ExportableProperty(prop, prop.PropertyType, null, colName));
            }
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
