using Cassandra;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Provides functionality to map and retrieve data from a ScyllaDB row to the properties of a specified result type.
/// </summary>
/// <remarks>This class facilitates mapping between the columns of a ScyllaDB row and the public instance
/// properties of the specified <typeparamref name="TResult"/> type. The mapping is cached for performance optimization,
/// ensuring that subsequent instances of the same result type reuse the cached mapping. If no matching columns are
/// found for the properties of <typeparamref name="TResult"/>, the mapping will be initialized as empty, allowing the
/// reader to be instantiated without mapping any data.</remarks>
/// <typeparam name="TResult">The type of the object to which the data will be mapped.</typeparam>
public class ScyllaDbObjectDataMapReader<TResult>
    :  IObjectMapReader<TResult>,  IScyllaDbObjectMapReader<TResult>
{
    static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, int>> _fieldIndexCache = [];
    IReadOnlyDictionary<string, int> _cachedFieldIndex;
    Row _dataReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScyllaDbObjectDataMapReader{TResult}"/> class, mapping the columns
    /// of the provided data reader to the properties of the specified result type.
    /// </summary>
    /// <remarks>This constructor creates a mapping between the column names in the provided <paramref
    /// name="dataReader"/> and the public instance properties of the <typeparamref name="TResult"/> type. The mapping
    /// is cached for performance optimization, and subsequent instances of the same result type will reuse the cached
    /// mapping.  If no matching columns are found for the properties of <typeparamref name="TResult"/>, the mapping
    /// will be initialized as empty. This ensures that the reader can still be instantiated, but no data will be
    /// mapped.</remarks>
    /// <param name="dataReader">The <see cref="Row"/> instance representing the data source to be read.  This parameter cannot be <see
    /// langword="null"/>.</param>
    public ScyllaDbObjectDataMapReader(Row dataReader)
    {
        _dataReader = dataReader;
        InitFieldIndexMap();
    }

    /// <summary>
    /// Advances the reader to the specified row without allocating a new instance.
    /// </summary>
    public ScyllaDbObjectDataMapReader<TResult> SetRow(Row row)
    {
        _dataReader = row;
        return this;
    }

    void InitFieldIndexMap()
    {
        _cachedFieldIndex = _fieldIndexCache.GetOrAdd(typeof(TResult), _ =>
        {
            var newMap = new Dictionary<string, int>();
            foreach (var e in typeof(TResult).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var column = _dataReader.GetColumn(e.Name);
                if (column is not null)
                    newMap.TryAdd(column.Name, column.Index);
            }
            return new ReadOnlyDictionary<string, int>(newMap);
        });
    }
    //readonly Row dataReader = dataReader;

    public Row Row => _dataReader;

    /// <summary>
    /// Retrieves the value of the specified property from the underlying data source.
    /// </summary>
    /// <param name="resultPropertyExpr">An expression that specifies the property to retrieve. The expression must be a lambda expression in the form of
    /// <c>x => x.PropertyName</c>, where <c>PropertyName</c> is the name of the property to retrieve.</param>
    /// <returns>The value of the specified property as a string.</returns>
    public string Get(Expression<Func<TResult, string>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.GetValue<string>(fieldIndex);
    }

    public bool Get(Expression<Func<TResult, bool>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return !_dataReader.IsNull(fieldIndex) && _dataReader.GetValue<bool>(fieldIndex);
    }

    public bool? Get(Expression<Func<TResult, bool?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return !_dataReader.IsNull(fieldIndex) ? _dataReader.GetValue<bool>(fieldIndex) : (bool?)null;
    }

    public int Get(Expression<Func<TResult, int>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<int>(fieldIndex);
    }

    public int? Get(Expression<Func<TResult, int?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (int?)null : _dataReader.GetValue<int>(fieldIndex);
    }

    public short Get(Expression<Func<TResult, short>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<short>(fieldIndex);
    }

    public short? Get(Expression<Func<TResult, short?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (short?)null : _dataReader.GetValue<short>(fieldIndex);
    }

    public long Get(Expression<Func<TResult, long>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<long>(fieldIndex);
    }

    public long? Get(Expression<Func<TResult, long?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (long?)null : _dataReader.GetValue<long>(fieldIndex);
    }

    public double Get(Expression<Func<TResult, double>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<double>(fieldIndex);
    }

    public double? Get(Expression<Func<TResult, double?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (double?)null : _dataReader.GetValue<double>(fieldIndex);
    }

    public float Get(Expression<Func<TResult, float>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<float>(fieldIndex);
    }

    public float? Get(Expression<Func<TResult, float?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (float?)null : _dataReader.GetValue<float>(fieldIndex);
    }

    public decimal Get(Expression<Func<TResult, decimal>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<decimal>(fieldIndex);
    }

    public decimal? Get(Expression<Func<TResult, decimal?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (decimal?)null : _dataReader.GetValue<decimal>(fieldIndex);
    }

    public DateTime Get(Expression<Func<TResult, DateTime>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<DateTime>(fieldIndex);
    }

    public DateTime? Get(Expression<Func<TResult, DateTime?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (DateTime?)null : _dataReader.GetValue<DateTime>(fieldIndex);
    }

    public TimeSpan Get(Expression<Func<TResult, TimeSpan>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : TimeSpan.FromTicks(_dataReader.GetValue<long>(fieldIndex));
    }

    public TimeSpan? Get(Expression<Func<TResult, TimeSpan?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (TimeSpan?)null : TimeSpan.FromTicks(_dataReader.GetValue<long>(fieldIndex));
    }

    public Guid Get(Expression<Func<TResult, Guid>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? default : _dataReader.GetValue<Guid>(fieldIndex);
    }

    public Guid? Get(Expression<Func<TResult, Guid?>> resultPropertyExpr)
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        return _dataReader.IsNull(fieldIndex) ? (Guid?)null : _dataReader.GetValue<Guid>(fieldIndex);
    }

    public TEnum Get<TEnum>(Expression<Func<TResult, TEnum>> resultPropertyExpr) where TEnum : struct
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        if (_dataReader.IsNull(fieldIndex)) return default;
        var s = _dataReader.GetValue<string>(fieldIndex);
        return Enum.TryParse<TEnum>(s, true, out var result) ? result : default;
    }

    public Enum Get<TEnum>(Expression<Func<TResult, Enum>> resultPropertyExpr) where TEnum : struct, Enum
    {
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        if (_dataReader.IsNull(fieldIndex)) return default(TEnum);
        var s = _dataReader.GetValue<string>(fieldIndex);
        return Enum.TryParse<TEnum>(s, true, out var result) ? result : default(TEnum);
    }

    // ScyllaDb specific types...
    public DateOnly Get(Expression<Func<TResult, DateOnly>> resultPropertyExpr)
        => GetDateOnly(resultPropertyExpr);

    public DateOnly? Get(Expression<Func<TResult, DateOnly?>> resultPropertyExpr)
    {
        var dateOnlyValue = GetDateOnly(resultPropertyExpr);
        return dateOnlyValue == default || dateOnlyValue == DateOnly.MinValue
            ? default
            : dateOnlyValue;
    }
   
    DateOnly GetDateOnly(LambdaExpression lambda)
    {
        var columnName = GetColumnName(lambda);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Date)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a Date type");
        return _dataReader.IsNull(GetFieldIndex(lambda))
           ? DateOnly.MinValue
           :  _dataReader.GetValue<DateOnly>( columnName);
    }

    public TimeOnly Get(Expression<Func<TResult, TimeOnly>> resultPropertyExpr)
        => GetTimeOnly(resultPropertyExpr);

    public TimeOnly? Get(Expression<Func<TResult, TimeOnly?>> resultPropertyExpr)
    {
        var timeOnlyValue = GetTimeOnly(resultPropertyExpr);
        return timeOnlyValue == default || timeOnlyValue == TimeOnly.MinValue
            ? default
            : timeOnlyValue;
    }
     
    TimeOnly GetTimeOnly(LambdaExpression lambda)
    {
        var columnName = GetColumnName(lambda);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Time)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a Time type");
        return _dataReader.GetValue<TimeOnly>(columnName);
    }

    public DateTime GetISODateTime(Expression<Func<TResult, DateTime>> resultPropertyExpr)
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Text)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a Text type");
        var stringValue = _dataReader.GetValue(typeof(string), columnName) as string;
        if (string.IsNullOrEmpty(stringValue))
            return default;
        return DateTime.ParseExact(stringValue, "O", null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public List<TResult>? Get(Expression<Func<TResult, List<TResult>>> resultPropertyExpr)
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.List)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a List type");
        return _dataReader.GetValue(typeof(List<TResult>), columnName) as List<TResult>;
    }

    public Dictionary<TKey, TResult>? Get<TKey>(Expression<Func<TResult, Dictionary<TKey, TResult>>> resultPropertyExpr) where TKey : notnull
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Map)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a Dictionary type");
        return _dataReader.GetValue(typeof(Dictionary<TKey, TResult>), columnName) as Dictionary<TKey, TResult>;
    }

    public HashSet<TResult>? Get(Expression<Func<TResult, HashSet<TResult>>> resultPropertyExpr)
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Set)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a HashSet type");
        return _dataReader.GetValue(typeof(HashSet<TResult>), columnName) as HashSet<TResult>;
    }

    public CqlVector<TResult>? Get(Expression<Func<TResult, CqlVector<TResult>>> resultPropertyExpr)
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Custom)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a HashSet type");
        return _dataReader.GetValue(typeof(CqlVector<TResult>), columnName) as CqlVector<TResult>;
    }

    public byte[] Get(Expression<Func<TResult, byte[]>> resultPropertyExpr)
    {
        var columnName = GetColumnName(resultPropertyExpr);
        var column = _dataReader.GetColumn(columnName);
        if (column.TypeCode != ColumnTypeCode.Blob)
            throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.Get: column '{column.Name}' is not a Blob");
        return _dataReader.GetValue<byte[]>(columnName);
    }

    int GetFieldIndex(LambdaExpression lambda)
    {
        var fieldName = ((lambda?.Body as MemberExpression)?.Member?.Name) ?? throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.GetFieldIndex: parameter MUST be a property from '{typeof(TResult).Name}'");
        if (!_cachedFieldIndex.TryGetValue(fieldName, out int fieldIndex))
        {
            var column = _dataReader.GetColumn(fieldName) ?? throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.GetFieldIndex: column '{fieldName}' not found in data reader");
            fieldIndex = column.Index;
        }
        return fieldIndex;
    }

    static string GetColumnName(LambdaExpression lambda)
        =>  ((lambda?.Body as MemberExpression)?.Member?.Name) ?? throw new InvalidOperationException($"ScyllaDbObjectDataMapReader.GetColumnName: parameter MUST be a property from '{typeof(TResult).Name}'");
   
}
