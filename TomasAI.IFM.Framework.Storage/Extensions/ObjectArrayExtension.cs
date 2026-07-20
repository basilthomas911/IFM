using System.Globalization;

namespace TomasAI.IFM.Framework.Storage.Extensions;

public static class ObjectArrayExtension
{
    /// <summary>
    /// Retrieves the integer value at the specified index from the given object array.
    /// </summary>
    public static int GetInt(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToInt32(objArray[index]);

    /// <summary>
    /// Retrieves the float value at the specified index from the given object array.
    /// </summary>
    public static float GetFloat(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToFloat(objArray[index]);

    /// <summary>
    /// Retrieves the double value at the specified index from the given object array.
    /// </summary>
    public static double GetDouble(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToDouble(objArray[index]);

    /// <summary>
    /// Retrieves the decimal value at the specified index from the given object array.
    /// </summary>
    public static decimal GetDecimal(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToDecimal(objArray[index]);

    /// <summary>
    /// Retrieves the boolean value at the specified index from the given object array.
    /// </summary>
    public static bool GetBool(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToBool(objArray[index]);

    /// <summary>
    /// Retrieves the long value at the specified index from the given object array.
    /// </summary>
    public static long GetLong(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToLong(objArray[index]);

    /// <summary>
    /// Retrieves the DateTime value at the specified index from the given object array.
    /// </summary>
    public static DateTime GetDateTime(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToDateTime(objArray[index]);

#if NET6_0_OR_GREATER
    /// <summary>
    /// Retrieves the DateOnly value at the specified index from the given object array.
    /// </summary>
    public static DateOnly GetDateOnly(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToDateOnly(objArray[index]);

    /// <summary>
    /// Retrieves the TimeOnly value at the specified index from the given object array.
    /// </summary>
    public static TimeOnly GetTimeOnly(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToTimeOnly(objArray[index]);
#endif

    /// <summary>
    /// Retrieves the enum value of type T at the specified index from the given object array.
    /// </summary>
    public static T GetEnum<T>(this object[] objArray, int index) where T : struct, Enum
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? default
            : ToEnum<T>(objArray[index]);

    /// <summary>
    /// Retrieves the string value at the specified index from the given object array.
    /// </summary>
    public static string GetString(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? string.Empty
            : objArray[index]?.ToString() ?? string.Empty;

    /// <summary>
    /// Retrieves the byte array value at the specified index from the given object array.
    /// </summary>
    public static byte[] GetBytes(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? Array.Empty<byte>()
            : ToBytes(objArray[index]);

    /// <summary>
    /// Retrieves the Guid value at the specified index from the given object array.
    /// </summary>
    public static Guid GetGuid(this object[] objArray, int index)
        => objArray is null || objArray.Length == 0 || index < 0 || index >= objArray.Length
            ? Guid.Empty
            : ToGuid(objArray[index]);

    static int ToInt32(object value) => value switch
    {
        int i => i,
        long l => (int)l,
        short s => s,
        string str when int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) => p,
        string => default,
        IConvertible c => c.ToInt32(CultureInfo.InvariantCulture),
        _ => default
    };

    static float ToFloat(object value) => value switch
    {
        float f => f,
        double d => (float)d,
        int i => i,
        long l => l,
        string str when float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
        string => default,
        IConvertible c => c.ToSingle(CultureInfo.InvariantCulture),
        _ => default
    };

    static double ToDouble(object value) => value switch
    {
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
        string => default,
        IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
        _ => default
    };

    static decimal ToDecimal(object value) => value switch
    {
        decimal m => m,
        double d => (decimal)d,
        float f => (decimal)f,
        int i => i,
        long l => l,
        string str when decimal.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) => p,
        string => default,
        IConvertible c => c.ToDecimal(CultureInfo.InvariantCulture),
        _ => default
    };

    static bool ToBool(object value) => value switch
    {
        bool b => b,
        int i => i != 0,
        string str when bool.TryParse(str, out var p) => p,
        _ => default
    };

    static long ToLong(object value) => value switch
    {
        long l => l,
        int i => i,
        string str when long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) => p,
        string => default,
        IConvertible c => c.ToInt64(CultureInfo.InvariantCulture),
        _ => default
    };

    static DateTime ToDateTime(object value)
    {
        if (value is DateTime dt)
            return dt;
        if (value is string s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        if (value is DateTimeOffset dto)
            return dto.DateTime;
        if (value is long l)
            return new DateTime(l);
        try { return Convert.ToDateTime(value, CultureInfo.InvariantCulture); } catch { return default; }
    }

#if NET6_0_OR_GREATER
    static DateOnly ToDateOnly(object value)
    {
        if (value is DateOnly d)
            return d;
        if (value is string s && DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        return default;
    }

    static TimeOnly ToTimeOnly(object value)
    {
        if (value is TimeOnly t) return t;
        if (value is string s && TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        return default;
    }
#endif

    static T ToEnum<T>(object value) where T : struct, Enum
    {
        if (value is T t)
            return t;
        if (value is string s && Enum.TryParse<T>(s, true, out var parsed))
            return parsed;
        if (value is int i && Enum.IsDefined(typeof(T), i))
            return (T)Enum.ToObject(typeof(T), i);
        return default;
    }

    static byte[] ToBytes(object value)
    {
        if (value is byte[] bytes)
            return bytes;
        if (value is string s)
        {
            var buf = new Span<byte>(new byte[s.Length]);
            return Convert.TryFromBase64String(s, buf, out var written)
                ? buf[..written].ToArray()
                : System.Text.Encoding.UTF8.GetBytes(s);
        }
        return [];
    }

    static Guid ToGuid(object value)
    {
        if (value is Guid guid)
            return guid;
        if (value is string s && Guid.TryParse(s, out var parsed))
            return parsed;
        if (value is byte[] bytes && bytes.Length == 16)
            return new Guid(bytes);
        return Guid.Empty;
    }
}
