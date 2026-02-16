using System.Reflection;

namespace Mockapala.Schema;

/// <summary>
/// Stores a one-way (to-database) conversion for a single property.
/// </summary>
public sealed class PropertyConversion
{
    internal PropertyConversion(string propertyName, MemberInfo member, Type convertedType, Func<object, object> converter)
    {
        PropertyName = propertyName;
        Member = member;
        ConvertedType = convertedType;
        Converter = converter;
    }

    /// <summary>The name of the property being converted.</summary>
    public string PropertyName { get; }

    /// <summary>The MemberInfo (PropertyInfo or FieldInfo) of the source property.</summary>
    public MemberInfo Member { get; }

    /// <summary>The CLR type that the converter produces.</summary>
    public Type ConvertedType { get; }

    /// <summary>Converts the original property value to the database-friendly value.</summary>
    public Func<object, object> Converter { get; }
}
