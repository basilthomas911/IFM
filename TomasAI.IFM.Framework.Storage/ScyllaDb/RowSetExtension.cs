using Cassandra;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Extension methods for Cassandra <see cref="RowSet"/> providing <see cref="IObjectDataRecord"/> access.
/// </summary>
public static class RowSetExtension
{
    /// <summary>
    /// Creates a reusable <see cref="IObjectDataRecord"/> adapter for the specified <see cref="RowSet"/>.
    /// Call <see cref="ScyllaDbDataRecord.SetRow"/> to advance to each row during iteration.
    /// </summary>
    public static ScyllaDbDataRecord ToObjectDataRecord(this RowSet rowSet)
        => new(rowSet.Columns);
}

/// <summary>
/// Wraps a Cassandra <see cref="Row"/> as an <see cref="IObjectDataRecord"/>, providing typed column
/// access without intermediate <c>object[]</c> allocation or boxing for value types.
/// </summary>
/// <remarks>
/// This class is designed for reuse across rows. Call <see cref="SetRow"/> to advance to the next row
/// without allocating a new instance. All accessor methods return the default value when the column
/// is null, matching the behaviour of <c>ObjectArrayExtension</c>.
/// </remarks>
public sealed class ScyllaDbDataRecord : IObjectDataRecord
{
    static class EnumCache<T> where T : struct, Enum
    {
        internal static readonly ConcurrentDictionary<string, T> Values = new(StringComparer.OrdinalIgnoreCase);
    }

    readonly CqlColumn[] _columns;
    Row _row = default!;

    internal ScyllaDbDataRecord(CqlColumn[] columns)
        => _columns = columns;

    /// <summary>
    /// Advances the adapter to the specified row. Zero-allocation per row.
    /// </summary>
    public ScyllaDbDataRecord SetRow(Row row)
    {
        _row = row;
        return this;
    }

    /// <inheritdoc />
    public int GetInt(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<int>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public float GetFloat(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<float>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public double GetDouble(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<double>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public decimal GetDecimal(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<decimal>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public bool GetBool(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<bool>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public long GetLong(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<long>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public DateTime GetDateTime(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<DateTime>(index); }
        catch
        {
            try
            {
                var value = _row.GetValue(_columns[index].Type, index);
                return value switch
                {
                    DateTimeOffset dto => dto.DateTime,
                    long l => new DateTime(l),
                    string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                    _ => default
                };
            }
            catch { return default; }
        }
    }

    /// <inheritdoc />
    public DateOnly GetDateOnly(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<DateOnly>(index); }
        catch
        {
            try
            {
                var value = _row.GetValue(_columns[index].Type, index);
                return value switch
                {
                    DateTime dt => DateOnly.FromDateTime(dt),
                    string s when DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                    _ => default
                };
            }
            catch { return default; }
        }
    }

    /// <inheritdoc />
    public TimeOnly GetTimeOnly(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<TimeOnly>(index); }
        catch
        {
            try
            {
                var value = _row.GetValue(_columns[index].Type, index);
                return value switch
                {
                    TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                    long ticks => new TimeOnly(ticks),
                    string s when TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                    _ => default
                };
            }
            catch { return default; }
        }
    }

    /// <inheritdoc />
    public T GetEnum<T>(int index) where T : struct, Enum
    {
        if (_row.IsNull(index)) return default;
        var colType = _columns[index].TypeCode;
        if (colType == ColumnTypeCode.Varchar || colType == ColumnTypeCode.Text)
        {
            var s = _row.GetValue<string>(index);
            return EnumCache<T>.Values.GetOrAdd(s, static str => Enum.TryParse<T>(str, true, out var p) ? p : default);
        }
        if (colType == ColumnTypeCode.Int)
        {
            var value = _row.GetValue<int>(index);
            return Unsafe.As<int, T>(ref Unsafe.AsRef(in value));
        }
        return default;
    }

    /// <inheritdoc />
    public Guid GetGuid(int index)
    {
        if (_row.IsNull(index)) return default;
        try { return _row.GetValue<Guid>(index); } catch { return default; }
    }

    /// <inheritdoc />
    public string GetString(int index)
    {
        if (_row.IsNull(index)) return string.Empty;
        try { return _row.GetValue<string>(index) ?? string.Empty; } catch { return string.Empty; }
    }

    /// <inheritdoc />
    public byte[] GetBytes(int index)
    {
        if (_row.IsNull(index)) return [];
        try { return _row.GetValue<byte[]>(index) ?? []; } catch { return []; }
    }
}
