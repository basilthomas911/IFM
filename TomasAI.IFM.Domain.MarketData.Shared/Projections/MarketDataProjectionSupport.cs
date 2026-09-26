namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Identifies the generation observed for one market-data projection scope.
/// </summary>
public readonly record struct MarketDataProjectionScopeGeneration(
    string ScopeKey,
    Guid Generation,
    bool IsMissing);

/// <summary>
/// Captures the global and scoped generations used to validate a projection read.
/// </summary>
public readonly record struct MarketDataProjectionScopeReadStamp(
    string ProjectionName,
    Guid GlobalGeneration,
    MarketDataProjectionScopeGeneration[] Scopes);

/// <summary>
/// Identifies the stage at which a guarded tick-projection operation failed.
/// </summary>
public enum TickProjectionGuardFailureStage
{
    RegistrationResponseUnknown,
    RegisteredBeforeDataSubmission,
    DataBatchResponseUnknown,
    AfterDataAcknowledged
}

/// <summary>
/// Represents a stable count and commutative fingerprint for a projection.
/// </summary>
public readonly record struct ProjectionIdentity(long Count, ulong Xor, ulong Sum)
{
    /// <summary>
    /// Gets the hexadecimal projection fingerprint.
    /// </summary>
    public string Fingerprint => $"{Xor:X16}{Sum:X16}";
}

/// <summary>
/// Accumulates a stable identity for an unordered projection result set.
/// </summary>
public sealed class ProjectionIdentityBuilder
{
    long _count;
    ulong _xor;
    ulong _sum;

    /// <summary>
    /// Adds one stable row hash to the projection identity.
    /// </summary>
    public void Add(ulong rowHash)
    {
        _count++;
        _xor ^= rowHash;
        _sum = unchecked(_sum + rowHash * 1099511628211UL);
    }

    /// <summary>
    /// Creates the accumulated projection identity.
    /// </summary>
    public ProjectionIdentity Build() => new(_count, _xor, _sum);
}

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
