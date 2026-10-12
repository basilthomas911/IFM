namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Provides stable hashing primitives for market-data projection identities.
/// </summary>
public static class MarketDataProjectionHash
{
    const ulong OffsetBasis = 14695981039346656037UL;
    const ulong Prime = 1099511628211UL;

    /// <summary>
    /// Creates the initial projection hash.
    /// </summary>
    public static ulong Start() => OffsetBasis;

    /// <summary>
    /// Adds a string value to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, string? value)
    {
        if (value is null)
            return AddByte(hash, 0xFE);

        foreach (var character in value)
        {
            hash = AddByte(hash, (byte)character);
            hash = AddByte(hash, (byte)(character >> 8));
        }
        return AddByte(hash, 0xFF);
    }

    /// <summary>
    /// Adds a date value to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, DateOnly value) => Add(hash, value.DayNumber);

    /// <summary>
    /// Adds a time value to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, TimeOnly value) => Add(hash, value.Ticks);

    /// <summary>
    /// Adds a 32-bit integer to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, int value) => Add(hash, unchecked((ulong)(uint)value));

    /// <summary>
    /// Adds a 64-bit integer to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, long value) => Add(hash, unchecked((ulong)value));

    /// <summary>
    /// Adds a floating-point value to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, double value) => Add(hash, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

    /// <summary>
    /// Adds a decimal value to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, decimal value)
        => Add(hash, value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds an unsigned 64-bit integer to a projection hash.
    /// </summary>
    public static ulong Add(ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
            hash = AddByte(hash, (byte)(value >> shift));
        return hash;
    }

    static ulong AddByte(ulong hash, byte value)
        => unchecked((hash ^ value) * Prime);
}
