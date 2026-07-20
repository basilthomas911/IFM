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
/// of <c>ObjectArrayExtension</c> and <see cref="ScyllaDb.ScyllaDbDataRecord"/>.
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
        try { return _reader.GetDateTime(index); }
        catch
        {
            try
            {
                var value = _reader.GetValue(index);
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
        if (_reader.IsDBNull(index)) return default;
        try { return DateOnly.FromDateTime(_reader.GetDateTime(index)); }
        catch
        {
            try
            {
                var value = _reader.GetValue(index);
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
        if (_reader.IsDBNull(index)) return default;
        try
        {
            var value = _reader.GetValue(index);
            return value switch
            {
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                DateTime dt => TimeOnly.FromDateTime(dt),
                long ticks => new TimeOnly(ticks),
                string s when TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
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
            var s = _reader.GetString(index);
            return Enum.TryParse<T>(s, true, out var parsed) ? parsed : default;
        }
        catch
        {
            try
            {
                var value = _reader.GetValue(index);
                return value switch
                {
                    T t => t,
                    int i when Enum.IsDefined(typeof(T), i) => (T)Enum.ToObject(typeof(T), i),
                    _ => default
                };
            }
            catch { return default; }
        }
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
        try { return _reader.GetString(index) ?? string.Empty; } catch { return string.Empty; }
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
