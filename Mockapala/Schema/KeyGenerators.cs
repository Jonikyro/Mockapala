using System.Globalization;

namespace Mockapala.Schema;

/// <summary>
/// Built-in key generators for common types.
/// </summary>
public static class KeyGenerators
{
    /// <summary>Sequential integer keys: 1, 2, 3, ...</summary>
    public static Func<int, int> SequentialInt => i => i;

    /// <summary>Sequential long keys: 1L, 2L, 3L, ...</summary>
    public static Func<int, long> SequentialLong => i => (long)i;

    /// <summary>New Guid per entity.</summary>
    public static Func<int, Guid> NewGuid => _ => System.Guid.NewGuid();

    /// <summary>Sequential string keys: "1", "2", "3", ...</summary>
    public static Func<int, string> SequentialString => i => i.ToString();

    /// <summary>
    /// String keys from a format pattern. Use {0} for the 1-based index.
    /// </summary>
    public static Func<int, string> StringFormat(string format)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));
        return i => string.Format(CultureInfo.InvariantCulture, format, i);
    }
}
