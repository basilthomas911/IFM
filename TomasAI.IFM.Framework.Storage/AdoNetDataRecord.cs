using System.Data;
using System.Globalization;

namespace TomasAI.IFM.Framework.Storage;

/// <summary>
/// Wraps an ADO.NET <see cref="IDataReader"/> as an <see cref="IObjectDataRecord"/>, providing typed column
/// access without intermediate <c>object[]</c> allocation or boxing for value types.
/// </summary>
/// <remarks>
/// This class is designed for reuse across rows. The underlying <see cref="IDataReader"/> is advanced
/// externally (via <c>Read()</c>); all accessor methods read from the current row position.
/// All accessors return the default value when the column is null, matching the behaviour
/// of <see cref="ScyllaDb.ScyllaDbDataRecord"/>.
/// </remarks>
public sealed class AdoNetDataRecord : IObjectDataRecord
{
    IDataReader _reader = default!;

    /// <summary>
    /// Sets the underlying <see cref="IDataReader"/>. Call once before iterating rows.
    /// </summary>
    public AdoNetDataRecord SetReader(IDataReader reader)
    {
        _reader = reader;
        return this;
    }

    /// <inheritdoc />
    public bool IsNull(int index) => _reader.IsDBNull(index);

    /// <inheritdoc />
    public bool IsCollectionEmpty(int index)
    {
        if (_reader.IsDBNull(index)) return true;
        try { return IsEmptyCollection(_reader.GetValue(index)); }
        catch { return false; }
    }

    static bool IsEmptyCollection(object value)
    {
        if (value is System.Collections.ICollection collection)
            return collection.Count == 0;
        if (value is not System.Collections.IEnumerable enumerable)
            return false;

        var enumerator = enumerable.GetEnumerator();
        try { return !enumerator.MoveNext(); }
        finally { (enumerator as IDisposable)?.Dispose(); }
    }

    /// <inheritdoc />
    public short GetShort(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetInt16(index); } catch { return default; }
    }

    /// <inheritdoc />
    public int GetInt(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetInt32(index); } catch { return default; }
    }

    /// <inheritdoc />
    public float GetFloat(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetFloat(index); } catch { return default; }
    }

    /// <inheritdoc />
    public double GetDouble(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetDouble(index); } catch { return default; }
    }

    /// <inheritdoc />
    public decimal GetDecimal(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetDecimal(index); } catch { return default; }
    }

    /// <inheritdoc />
    public bool GetBool(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetBoolean(index); } catch { return default; }
    }

    /// <inheritdoc />
    public long GetLong(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetInt64(index); } catch { return default; }
    }

    /// <inheritdoc />
    public DateTime GetDateTime(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                DateTime dateTime => dateTime,
                DateOnly date => date.ToDateTime(TimeOnly.MinValue),
                DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
                long ticks when ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks => new DateTime(ticks),
                string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                _ => default
            };
        }
        catch { return default; }
    }

    /// <inheritdoc />
    public DateOnly GetDateOnly(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                DateOnly date => date,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
                string text when DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                _ => default
            };
        }
        catch { return default; }
    }

    /// <inheritdoc />
    public TimeOnly GetTimeOnly(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                TimeOnly time => time,
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                DateTime dt => TimeOnly.FromDateTime(dt),
                long ticks when ticks >= TimeOnly.MinValue.Ticks && ticks <= TimeOnly.MaxValue.Ticks => new TimeOnly(ticks),
                string s when TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                _ => default
            };
        }
        catch { return default; }
    }

    /// <inheritdoc />
    public TimeSpan GetTimeSpan(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                TimeSpan timeSpan => timeSpan,
                TimeOnly timeOnly => timeOnly.ToTimeSpan(),
                long ticks => new TimeSpan(ticks),
                string text when TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => default
            };
        }
        catch { return default; }
    }

    /// <inheritdoc />
    public T GetEnum<T>(int index) where T : struct, Enum
    {
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                T enumValue => enumValue,
                string text when Enum.TryParse<T>(text, true, out var parsed) => parsed,
                int numericValue => GetDefinedEnumValue<T>(numericValue),
                _ => default
            };
        }
        catch { return default; }
    }

    static T GetDefinedEnumValue<T>(int value) where T : struct, Enum
    {
        var enumValue = (T)Enum.ToObject(typeof(T), value);
        return Enum.IsDefined(enumValue) ? enumValue : default;
    }

    /// <inheritdoc />
    public Guid GetGuid(int index)
    {
        if (_reader.IsDBNull(index)) return default;
        try { return _reader.GetGuid(index); } catch { return default; }
    }

    /// <inheritdoc />
    public string GetString(int index)
    {
        if (_reader.IsDBNull(index)) return string.Empty;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                string text => text,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }
        catch { return string.Empty; }
    }

    /// <inheritdoc />
    public byte[] GetBytes(int index)
    {
        if (_reader.IsDBNull(index)) return [];
        try
        {
            var length = _reader.GetBytes(index, 0, null, 0, 0);
            if (length == 0) return [];
            var buffer = new byte[length];
            _reader.GetBytes(index, 0, buffer, 0, (int)length);
            return buffer;
        }
        catch { return []; }
    }
}
