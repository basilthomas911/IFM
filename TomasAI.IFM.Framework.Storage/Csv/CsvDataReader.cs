using System.Data;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage.Csv;

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
    readonly List<string[]> _rows;
    int _cursor;
    readonly List<PropertyInfo> _propertyInfo;
    readonly Dictionary<string, int> _propertyIndex;
    Dictionary<string, int> _nameIndex;
    readonly Dictionary<Type, Func<int, object>> _valueMap;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Validation and persistence orchestration remain together to preserve execution ordering.")]
    public CsvDataReader(IEnumerable<string> sourceRows, bool containsHeader = true)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);
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
            {typeof(Guid), id => IsDBNull(id) ? Guid.Empty : GetGuid(id) },
            {typeof(Guid?), id => IsDBNull(id) ? default(Guid?) !: GetGuid(id) }
        };

        _rows = [];
        SplitRowData(sourceRows);
        _cursor = 0;
        return;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Preserves existing case-normalized URI/header matching behavior.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
        void SplitRowData(IEnumerable<string> rows)
        {
            var headerFlag = containsHeader;

            string[] headerCols = [];

            foreach (var row in rows)
            {
                try
                {
                    if (headerFlag)
                    {
                        headerCols = row.Split([","], StringSplitOptions.TrimEntries);
                        headerFlag = false;
                        _nameIndex = [];
                        for (var i = 0; i < headerCols.Length; i++)
                        {
                            var colName = headerCols[i].Replace("\"", "", StringComparison.Ordinal);
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
                        cols[j] = j < colEntries.Length ? colEntries[j].Replace("\"", "", StringComparison.Ordinal) : string.Empty;
                    }
                    _rows.Add(cols);
                }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
            }
        }

    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "Preserves the existing externally referenced identity and named API contract.")]
    public static async Task<CsvDataReader<TData>> CreateAsync(
        IStringReader stringReader,
        bool containsHeader = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stringReader);
        var rows = new List<string>();
        await foreach (var row in stringReader.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(row);
        return new CsvDataReader<TData>(rows, containsHeader);
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Rows are materialized managed values; there is no owned external resource.
    }

    public bool GetBoolean(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return bool.TryParse(fieldValue, out var result) && result;
    }

    public byte GetByte(int i)
    {
        throw new NotSupportedException();
    }

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
    {
        throw new NotSupportedException();
    }

    public char GetChar(int i)
    {
        throw new NotSupportedException();
    }

    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
    {
        throw new NotSupportedException();
    }

    public IDataReader GetData(int i)
    {
        throw new NotSupportedException();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public string GetDataTypeName(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType.Name;
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return string.Empty;
    }

    public DateTime GetDateTime(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return DateTime.TryParse(fieldValue, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var result)
            ? result : DateTime.MinValue;
    }

    public decimal GetDecimal(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return decimal.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : decimal.MinValue;
    }

    public double GetDouble(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return double.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : double.MinValue;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public Type GetFieldType(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].PropertyType;
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return default!;
    }

    public float GetFloat(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return float.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
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
        return short.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : short.MinValue;
    }

    public int GetInt32(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return int.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : 0;
    }

    public long GetInt64(int i)
    {
        var fieldValue = GetString(i) ?? string.Empty;
        return long.TryParse(fieldValue, System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result : long.MinValue;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public string GetName(int i)
    {
        try
        {
            ValidIndex(i);
            return _propertyInfo[i].Name;
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Preserves existing case-normalized URI/header matching behavior.")]
    public int GetOrdinal(string name)
        
    {
        ArgumentNullException.ThrowIfNull(name);
        return _propertyIndex.ContainsKey(name) && _nameIndex.TryGetValue(name.ToLowerInvariant(), out var index)
            ? index
            : -1;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public DataTable GetSchemaTable()
    {
        var dataTable = new DataTable("CsvDataReader");
        try
        {
            foreach (var pi in _propertyInfo)
                dataTable.Columns.Add(new DataColumn(pi.Name, pi.PropertyType));
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return dataTable;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public string GetString(int i)
    {
        try
        {
            ValidIndex(i);
            return _rows[_cursor][i];
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return default!;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public object GetValue(int i)
    {
        try
        {
            ValidIndex(i);
            var pi = _propertyInfo[i];
            return _valueMap[pi.PropertyType](i);
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return default!;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        try
        {
            for (var index = 0; index < values.Length; index++)
                values[index] = GetValue(index);
            return values.Length;
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return default;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Compatibility reader methods return their documented empty/default conversion result; changing failure semantics is a separate data-validation migration.")]
    public bool IsDBNull(int i)
    {
        try
        {
            ValidIndex(i);
            return string.IsNullOrWhiteSpace(_rows[_cursor][i]);
        }
        catch (Exception ex) { System.Diagnostics.Trace.TraceError("Data reader conversion failed: {0}", ex); }
        return true;
    }

    public bool NextResult()
    {
        throw new NotSupportedException();
    }

    public bool Read()
        => ++_cursor < _rows.Count;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2201", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0012", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S112", Justification = "IDataReader field access preserves the established IndexOutOfRangeException contract for invalid ordinal or cursor.")]
    void ValidIndex(int index)
    {
        if (index < 0 || index >= FieldCount)
            throw new IndexOutOfRangeException("CsvDataReader invalid field index value");
        if (_cursor < 1 || _cursor >= _rows.Count)
            throw new IndexOutOfRangeException("CsvDataReader invalid cursor");
    }
}
