using System.Data;
using System.Linq.Expressions;

namespace TomasAI.IFM.Framework.Storage;

public class ObjectDataMapReader<TResult>(IDataReader dataReader)
    : IObjectMapReader<TResult>
{
    readonly IDataReader _dataReader = dataReader;
    readonly Dictionary<string, int> _fieldIndexMap = [];

    public TMapper? As<TMapper>() where TMapper : class
        => this as TMapper;

    public string Get(Expression<Func<TResult, string>> resultPropertyExpr)
        => _dataReader.GetString(GetFieldIndex(resultPropertyExpr));

    public bool Get(Expression<Func<TResult, bool>> resultPropertyExpr)
        => _dataReader.GetBoolean(GetFieldIndex(resultPropertyExpr));

    public bool? Get(Expression<Func<TResult, bool?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr)) 
            ? (bool?)null 
            : _dataReader.GetBoolean(GetFieldIndex(resultPropertyExpr));

    public int Get(Expression<Func<TResult, int>> resultPropertyExpr)
        => _dataReader.GetInt32(GetFieldIndex(resultPropertyExpr));

    public int? Get(Expression<Func<TResult, int?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (int?)null
            : _dataReader.GetInt32(GetFieldIndex(resultPropertyExpr));

    public short Get(Expression<Func<TResult, short>> resultPropertyExpr)
        => _dataReader.GetInt16(GetFieldIndex(resultPropertyExpr));

    public short? Get(Expression<Func<TResult, short?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (short?)null
            : _dataReader.GetInt16(GetFieldIndex(resultPropertyExpr));

    public long Get(Expression<Func<TResult, long>> resultPropertyExpr)
        => _dataReader.GetInt64(GetFieldIndex(resultPropertyExpr));

    public long? Get(Expression<Func<TResult, long?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (long?)null
            : _dataReader.GetInt64(GetFieldIndex(resultPropertyExpr));

    public double Get(Expression<Func<TResult, double>> resultPropertyExpr)
        => _dataReader.GetDouble(GetFieldIndex(resultPropertyExpr));

    public double? Get(Expression<Func<TResult, double?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (double?)null
            : _dataReader.GetDouble(GetFieldIndex(resultPropertyExpr));

    public float Get(Expression<Func<TResult, float>> resultPropertyExpr)
        => _dataReader.GetFloat(GetFieldIndex(resultPropertyExpr));

    public float? Get(Expression<Func<TResult, float?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (float?)null
            : _dataReader.GetFloat(GetFieldIndex(resultPropertyExpr));

    public decimal Get(Expression<Func<TResult, decimal>> resultPropertyExpr)
        => _dataReader.GetDecimal(GetFieldIndex(resultPropertyExpr));

    public decimal? Get(Expression<Func<TResult, decimal?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (decimal?)null
            : _dataReader.GetDecimal(GetFieldIndex(resultPropertyExpr));

    public DateTime Get(Expression<Func<TResult, DateTime>> resultPropertyExpr)
        => _dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr));

    public DateTime? Get(Expression<Func<TResult, DateTime?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (DateTime?)null
            : _dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr));

    public DateOnly Get(Expression<Func<TResult, DateOnly>> resultPropertyExpr)
        =>  DateOnly.FromDateTime(_dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr)));

    public DateOnly? Get(Expression<Func<TResult, DateOnly?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (DateOnly?)null
            : DateOnly.FromDateTime(_dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr)));

    public TimeSpan Get(Expression<Func<TResult, TimeSpan>> resultPropertyExpr)
        => TimeSpan.FromTicks(_dataReader.GetInt64(GetFieldIndex(resultPropertyExpr)));

    public TimeSpan? Get(Expression<Func<TResult, TimeSpan?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (TimeSpan?)null
            : TimeSpan.FromTicks(_dataReader.GetInt64(GetFieldIndex(resultPropertyExpr)));

    public Guid Get(Expression<Func<TResult, Guid>> resultPropertyExpr)
        => _dataReader.GetGuid(GetFieldIndex(resultPropertyExpr));

    public Guid? Get(Expression<Func<TResult, Guid?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (Guid?)null
            : _dataReader.GetGuid(GetFieldIndex(resultPropertyExpr));

    public TEnum Get<TEnum>(Expression<Func<TResult, TEnum>> resultPropertyExpr) where TEnum : struct
    {
        if (_dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr)))
            return default;
        var enumValue = _dataReader.GetString(GetFieldIndex(resultPropertyExpr));
        if (string.IsNullOrEmpty(enumValue))
            return default;
        if (Enum.TryParse<TEnum>(enumValue, out var parsedEnum))
            return parsedEnum;
        return default;
    }

    public TimeOnly Get(Expression<Func<TResult, TimeOnly>> resultPropertyExpr)
       => TimeOnly.FromDateTime(_dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr)));

    public TimeOnly? Get(Expression<Func<TResult, TimeOnly?>> resultPropertyExpr)
        => _dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr))
            ? (TimeOnly?)null
            : TimeOnly.FromDateTime(_dataReader.GetDateTime(GetFieldIndex(resultPropertyExpr)));

    public Enum Get<TEnum>(Expression<Func<TResult, Enum>> resultPropertyExpr) where TEnum : struct, Enum
    {
        throw new NotImplementedException();
    }

    public byte[] Get(Expression<Func<TResult, byte[]>> resultPropertyExpr)
    {
        var byteArray = default(byte[]);
        if (_dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr)))
            return byteArray!;
        var fieldIndex = GetFieldIndex(resultPropertyExpr);
        var byteArrayLength = _dataReader.GetBytes(fieldIndex, 0, byteArray, 0, 0);
        if (byteArrayLength == 0)
            return byteArray!;
        byteArray = new byte[byteArrayLength];
        _dataReader.GetBytes(fieldIndex, 0, byteArray, 0, (int)byteArrayLength);
        return byteArray;
    }

    public DateTime GetISODateTime(Expression<Func<TResult, DateTime>> resultPropertyExpr)
    {
        if (_dataReader.IsDBNull(GetFieldIndex(resultPropertyExpr)))
            return default;
        var stringValue = _dataReader.GetString(GetFieldIndex(resultPropertyExpr)); 
        return DateTime.ParseExact(stringValue, "O", null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    int GetFieldIndex(LambdaExpression lambda)
    {
        var fieldName = (lambda?.Body as MemberExpression)?.Member?.Name;
        if (fieldName is null)
            throw new InvalidOperationException($"ObjectDataMapReader.GetFieldIndex: parameter must be a property from '{typeof(TResult).Name}'");
        if (_fieldIndexMap.TryGetValue(fieldName, out int fieldIndex))
            return fieldIndex;
        fieldIndex = _dataReader.GetOrdinal(fieldName);
        if (fieldIndex < 0)
            throw new InvalidOperationException($"ObjectDataMapReader.GetFieldIndex: field '{fieldName}' not found in data reader for type '{typeof(TResult).Name}'");
        _fieldIndexMap.Add(fieldName, fieldIndex);
        return fieldIndex;
    }

    /// <summary>
    /// return property name from property expression
    /// </summary>
    /// <param name="propertyExpr"></param>
    /// <returns></returns>
    string GetPropertyName(LambdaExpression lambda)
    {
        if (lambda == null)
            throw new ArgumentException($"ObjectDataReader.GetPropertyName: parameter MUST be a property from '{typeof(TResult).Name}'");
        var memberExpr = lambda.Body as MemberExpression;
        if (memberExpr == null)
            throw new InvalidOperationException($"ObjectDataReader.GetPropertyName: parameter MUST be a property from '{typeof(TResult).Name}'");
        return memberExpr.Member.Name;
    }
}
