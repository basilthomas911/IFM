using System.Linq.Expressions;
using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public class ScyllaDbObjectDataMapper<TResult>
    : IObjectDataMapper<TResult> where TResult : class
{
    public static IObjectDataMapper<TMapper> Create<TMapper>(Row row) where TMapper : class
    {
        if (row == null)
            throw new ArgumentNullException(nameof(row), "ScyllaDbObjectDataMapper: Row cannot be null");
        return new ScyllaDbObjectDataMapper<TMapper>(row);
    }

    readonly Row _row;
    private ScyllaDbObjectDataMapper(Row row)
    {
        _row = row;
    }

    public TResult As() => default!;

    public string GetString(string columnName) => _row.GetValue<string>(columnName);
    public bool GetBool(string columnName) => _row.GetValue<bool>(columnName);
    public bool? GetNullableBool(string columnName) => _row.IsNull(columnName) ? (bool?)null : _row.GetValue<bool>(columnName);
    public int GetInt(string columnName) => _row.GetValue<int>(columnName);
    public int? GetNullableInt(string columnName) => _row.IsNull(columnName) ? (int?)null : _row.GetValue<int>(columnName);
    public short GetShort(string columnName) => _row.GetValue<short>(columnName);
    public short? GetNullableShort(string columnName) => _row.IsNull(columnName) ? (short?)null : _row.GetValue<short>(columnName);
    public long GetLong(string columnName) => _row.GetValue<long>(columnName);
    public long? GetNullableLong(string columnName) => _row.IsNull(columnName) ? (long?)null : _row.GetValue<long>(columnName);
    public double GetDouble(string columnName) => _row.GetValue<double>(columnName);
    public double? GetNullableDouble(string columnName) => _row.IsNull(columnName) ? (double?)null : _row.GetValue<double>(columnName);
    public float GetFloat(string columnName) => _row.GetValue<float>(columnName);
    public float? GetNullableFloat(string columnName) => _row.IsNull(columnName) ? (float?)null : _row.GetValue<float>(columnName);
    public decimal GetDecimal(string columnName) => _row.GetValue<decimal>(columnName);
    public decimal? GetNullableDecimal(string columnName) => _row.IsNull(columnName) ? (decimal?)null : _row.GetValue<decimal>(columnName);
    public DateTime GetDateTime(string columnName) => _row.GetValue<DateTime>(columnName);
    public DateTime? GetNullableDateTime(string columnName) => _row.IsNull(columnName) ? (DateTime?)null : _row.GetValue<DateTime>(columnName);
    public TimeSpan GetTimeSpan(string columnName) => _row.GetValue<TimeSpan>(columnName);
    public TimeSpan? GetNullableTimeSpan(string columnName) => _row.IsNull(columnName) ? (TimeSpan?)null : _row.GetValue<TimeSpan>(columnName);
    public DateOnly GetDateOnly(string columnName) => _row.GetValue<DateOnly>(columnName);
    public DateOnly? GetNullableDateOnly(string columnName) => _row.IsNull(columnName) ? (DateOnly?)null : _row.GetValue<DateOnly>(columnName);
    public TimeOnly GetTimeOnly(string columnName) => _row.GetValue<TimeOnly>(columnName);
    public TimeOnly? GetNullableTimeOnly(string columnName) => _row.IsNull(columnName) ? (TimeOnly?)null : _row.GetValue<TimeOnly>(columnName);
    public Guid GetGuid(string columnName) => _row.GetValue<Guid>(columnName);
    public Guid? GetNullableGuid(string columnName) => _row.IsNull(columnName) ? (Guid?)null : _row.GetValue<Guid>(columnName);
    public byte[] GetBytes(string columnName) => _row.GetValue<byte[]>(columnName);
    public TEnum GetEnum<TEnum>(string columnName) where TEnum : struct, Enum => (TEnum)Enum.Parse(typeof(TEnum), _row.GetValue<string>(columnName));
    public TStruct GetStruct<TStruct>(string columnName) where TStruct : struct => _row.GetValue<TStruct>(columnName);
    public DateTime GetISODateTime(string columnName) => _row.GetValue<DateTime>(columnName); // Adjust if special ISO handling needed
}
