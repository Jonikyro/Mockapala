using System.Data;
using System.Reflection;

namespace Mockapala.Export.SqlBulkCopy;

/// <summary>
/// IDataReader that streams rows from an in-memory entity list for SqlBulkCopy.WriteToServer(IDataReader).
/// </summary>
internal sealed class EntityDataReader : IDataReader
{
    private readonly IReadOnlyList<object> _entities;
    private readonly IReadOnlyList<PropertyInfo> _properties;
    private int _currentIndex = -1;
    private bool _closed;

    public EntityDataReader(
        IReadOnlyList<object> entities,
        IReadOnlyList<PropertyInfo> properties)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    public int FieldCount => _properties.Count;

    public object this[int i] => GetValue(i);

    public object this[string name] => GetValue(GetOrdinal(name));

    public bool Read()
    {
        if (_closed) return false;
        _currentIndex++;
        return _currentIndex < _entities.Count;
    }

    public object GetValue(int i)
    {
        if (i < 0 || i >= _properties.Count)
            throw new IndexOutOfRangeException(nameof(i));
        if (_currentIndex < 0 || _currentIndex >= _entities.Count)
            throw new InvalidOperationException("No current row. Call Read() first.");
        var value = _properties[i].GetValue(_entities[_currentIndex]);
        return value ?? DBNull.Value;
    }

    public string GetName(int i)
    {
        if (i < 0 || i >= _properties.Count)
            throw new IndexOutOfRangeException(nameof(i));
        return _properties[i].Name;
    }

    public Type GetFieldType(int i)
    {
        if (i < 0 || i >= _properties.Count)
            throw new IndexOutOfRangeException(nameof(i));
        var t = _properties[i].PropertyType;
        return Nullable.GetUnderlyingType(t) ?? t;
    }

    public int GetOrdinal(string name)
    {
        for (var i = 0; i < _properties.Count; i++)
            if (string.Equals(_properties[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        throw new IndexOutOfRangeException(nameof(name));
    }

    public bool IsDBNull(int i)
    {
        var value = GetValue(i);
        return value == null || value == DBNull.Value;
    }

    public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
    public byte GetByte(int i) => Convert.ToByte(GetValue(i));
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => 0;
    public char GetChar(int i) => Convert.ToChar(GetValue(i));
    public long GetChars(int i, long fieldOffset, char[]? buffer, int bufferoffset, int length) => 0;
    public IDataReader GetData(int i) => null!;
    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
    public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
    public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
    public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
    public Guid GetGuid(int i) => (Guid)GetValue(i)!;
    public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
    public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
    public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
    public string GetString(int i) => (string)GetValue(i)!;
    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, _properties.Count);
        for (var i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }

    public void Close() => _closed = true;

    public bool IsClosed => _closed;

    public void Dispose() => Close();

    public bool NextResult() => false;

    public int Depth => 0;

    public DataTable? GetSchemaTable()
    {
        var table = new DataTable();
        table.Columns.Add("ColumnName", typeof(string));
        table.Columns.Add("DataType", typeof(Type));
        table.Columns.Add("ColumnOrdinal", typeof(int));
        for (var i = 0; i < _properties.Count; i++)
        {
            var row = table.NewRow();
            row["ColumnName"] = GetName(i);
            row["DataType"] = GetFieldType(i);
            row["ColumnOrdinal"] = i;
            table.Rows.Add(row);
        }
        return table;
    }

    public int RecordsAffected => -1;
}
