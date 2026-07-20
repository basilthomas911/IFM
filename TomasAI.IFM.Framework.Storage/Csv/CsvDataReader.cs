using System.Data;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage.Csv;

public interface ICsvDataReader : IDataReader
{
}

/// <summary>
/// Provides a data reader implementation for reading CSV data into objects of type <typeparamref name="TData"/>.
/// </summary>
/// <remarks>This class implements <see cref="ICsvDataReader"/> and provides methods to read and access CSV data
/// row by row. It supports mapping CSV columns to properties of the specified <typeparamref name="TData"/> type based
/// on property names.  The reader assumes that the first row of the CSV contains column headers, which are used to map
/// data to the properties of <typeparamref name="TData"/>. The class provides methods to retrieve data in various
/// formats (e.g., <see cref="GetString(int)"/>, <see cref="GetInt32(int)"/>), and handles type conversions where
/// possible. If a conversion fails, an <see cref="InvalidCastException"/> is thrown.</remarks>
/// <typeparam name="TData">The type of objects to map the CSV data to. Each row in the CSV is mapped to an instance of this type.</typeparam>
public class CsvDataReader<TData> : ICsvDataReader
{
    List<string[]> _rows;
    int _cursor;
    List<PropertyInfo> _propertyInfo;
    Dictionary<string, int> _propertyIndex;
    Dictionary<string, int> _nameIndex;
    Dictionary<Type, Func<int, object>> _valueMap;

    public CsvDataReader(IStringReader stringReader, bool containsHeader = true)
    {
        _propertyInfo = [.. typeof(TData).GetProperties()];
        _propertyIndex = [];
        for (var index = 0; index < _propertyInfo.Count; index++)
            _propertyIndex.Add(_propertyInfo[index].Name, index);

        _valueMap = new Dictionary<Type, Func<int, object>>
        {
            {typeof(string), id => IsDBNull(id) ? default! : GetString(id) },
            {typeof(bool), id => IsDBNull(id) ? default : GetBoolean(id) },
            {typeof(bool?), id => IsDBNull(id) ? default(bool?)! : GetBoolean(id) },
            {typeof(int), id => IsDBNull(id) ? default : GetInt32(id) },
            {typeof(int?), id => IsDBNull(id) ? default(int?) !: GetInt32(id) },
            {typeof(short), id => IsDBNull(id) ? default : GetInt16(id) },
            {typeof(short?), id => IsDBNull(id) ? default(short?)! : GetInt16(id) },
            {typeof(long), id => IsDBNull(id) ? default : GetInt64(id) },
            {typeof(long?), id => IsDBNull(id) ? default(long?) !: GetInt64(id) },
            {typeof(ulong), id => IsDBNull(id) ? default : Convert.ToUInt64( GetInt64(id)) },
            {typeof(ulong?), id => IsDBNull(id) ? default(ulong?) !: Convert.ToUInt64( GetInt64(id)) },
            {typeof(double), id => IsDBNull(id) ? default : GetDouble(id) },
            {typeof(double?), id => IsDBNull(id) ? default(double?) !: GetDouble(id) },
            {typeof(float), id => IsDBNull(id) ? default : GetFloat(id) },
            {typeof(float?), id => IsDBNull(id) ? default(float?) !: GetFloat(id) },
            {typeof(decimal), id => IsDBNull(id) ? default : GetDecimal(id) },
            {typeof(decimal?), id => IsDBNull(id) ? default(decimal?) !: GetDecimal(id) },
            {typeof(DateTime), id => IsDBNull(id) ? default : GetDateTime(id) },
            {typeof(DateTime?), id => IsDBNull(id) ? default(DateTime?) !: GetDateTime(id) },
            {typeof(Guid), id => IsDBNull(id) ? default : GetGuid(id) },
            {typeof(Guid?), id => IsDBNull(id) ? default(Guid?) !: GetGuid(id) }
        };

        _rows = [];
        /*
        var rowData = stringReader.ReadToEndAsync().Result;
        if (!string.IsNullOrEmpty(rowData))
            SplitRowData();
        */
        SplitRowData();
        _cursor = 0;
        return;

        void SplitRowData()
        {
            var headerFlag = containsHeader;
            //var rows = rowData.Split([ "\r\n"], StringSplitOptions.RemoveEmptyEntries);
            string[] headerCols = [];

            var readLinesTask = stringReader.ReadLinesAsync().GetAsyncEnumerator();
            try
            {
                while (readLinesTask.MoveNextAsync().AsTask().Result)
                {
                    var row = readLinesTask.Current;
                    try
                    {
                        if (headerFlag)
                        {
                            headerCols = row.Split([","], StringSplitOptions.TrimEntries);
                            headerFlag = false;
                            _nameIndex = [];
                            for (var i = 0; i < headerCols.Length; i++)
                            {
                                var colName = headerCols[i].Replace("\"","");
                                if (!_nameIndex.ContainsKey(colName.ToLowerInvariant()))
                                {
                                    _nameIndex.Add(colName.ToLowerInvariant(), i);
                                }
                            }
                            continue;
                        }
                        var cols = new string[headerCols.Length];
                        var colEntries = row.Split([","], StringSplitOptions.TrimEntries);
                        for (var j = 0; j < headerCols.Length; j++)
                        {
                            cols[j] = j < colEntries.Length ? colEntries[j].Replace("\"", "") : string.Empty;
                        }
                        _rows.Add(cols);
                    }
                    catch { }
                }
            }
            finally
            {
                readLinesTask.DisposeAsync().AsTask().Wait();
            }
        }

    }

    public object this[int i] => GetValue(i);

    public object this[string name] => GetValue(GetOrdinal(name));

    public int Depth => 0;

    public bool IsClosed => false;

    public int RecordsAffected => 0;

    public int FieldCount => _propertyInfo.Count;

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public bool GetBoolean(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return bool.TryParse(fieldValue, out var result) && result;
    }

    public byte GetByte(int i)
    {
        throw new NotImplementedException();
    }

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
    {
        throw new NotImplementedException();
    }

    public char GetChar(int i)
    {
        throw new NotImplementedException();
    }

    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
    {
        throw new NotImplementedException();
    }

    public IDataReader GetData(int i)
    {
        throw new NotImplementedException();
    }

    public string GetDataTypeName(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType.Name;
        }
        catch { }
        return string.Empty;    
    }

    public DateTime GetDateTime(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return DateTime.TryParse(fieldValue, out var result)
            ? result : DateTime.MinValue;
    }

    public decimal GetDecimal(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return decimal.TryParse(fieldValue, out var result)
            ? result : decimal.MinValue;
    }

    public double GetDouble(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return double.TryParse(fieldValue, out var result)
            ? result : double.MinValue;
    }

    public Type GetFieldType(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType;
        }
        catch { }
        return default!;
    }

    public float GetFloat(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return float.TryParse(fieldValue, out var result)
            ? result : float.MinValue;
    }

    public Guid GetGuid(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return Guid.TryParse(fieldValue, out var result)
            ? result : Guid.Empty;
    }

    public short GetInt16(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return short.TryParse(fieldValue, out var result)
            ? result : short.MinValue;
    }

    public int GetInt32(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return int.TryParse(fieldValue, out var result)
            ? result : 0;
    }

    public long GetInt64(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return long.TryParse(fieldValue, out var result)
            ? result : long.MinValue;
    }

    public string GetName(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].Name;
        }
        catch { }
        return string.Empty;
    }

    public int GetOrdinal(string name)
        => _propertyIndex.ContainsKey(name) && _nameIndex.TryGetValue(name.ToLowerInvariant(), out var index)
            ? index
            : -1;
    
    public DataTable GetSchemaTable()
    {
        var dataTable = new DataTable("CsvDataReader");
        try
        {
            foreach (var pi in _propertyInfo)
                dataTable.Columns.Add(new DataColumn(pi.Name, pi.PropertyType));
        }
        catch { }
        return dataTable;
    }

    public string GetString(int i)
    {
        try
        {
            ValidIndex(i);
            return _rows[_cursor][i];
        }
        catch { }
        return default!;
    }

    public object GetValue(int i)
    {
        try
        {
            ValidIndex(i);
            var pi = _propertyInfo[i];
            return _valueMap[pi.PropertyType](i);
        }
        catch { }
        return default!;
    }

    public int GetValues(object[] values)
    {
        try
        {
            for (var index = 0; index < values.Length; index++)
                values[index] = GetValue(index);
            return values.Length;
        }
        catch { }
        return default;
    }

    public bool IsDBNull(int i)
    {
        try
        {
            ValidIndex(i);
            return string.IsNullOrWhiteSpace(_rows[_cursor][i]);
        }
        catch { }
        return true;
    }

    public bool NextResult()
    {
        throw new NotImplementedException();
    }

    public bool Read() 
        => ++_cursor < _rows.Count;

    void ValidIndex(int index)
    {
        if (index < 0 || index >= FieldCount)
            throw new IndexOutOfRangeException("CsvDataReader invalid field index value");
        if (_cursor < 1 || _cursor >= _rows.Count)
            throw new IndexOutOfRangeException("CsvDataReader invalid cursor");
    }
}
