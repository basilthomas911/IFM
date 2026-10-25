using MessagePack;

namespace TomasAI.IFM.Shared.MarketData.Contracts;

/// <summary>Failure has no usable rate; callers must not substitute zero or a neighboring tenor.</summary>
/// <param name="Value">The value.</param>
/// <param name="Error">The error.</param>
public sealed record TreasuryContinuousRateResult(TreasuryContinuousRate? Value, string? Error)
{
    public bool Succeeded => Value is not null && Error is null;
    /// <summary>Creates an unsuccessful result containing the supplied failure detail.</summary>
    /// <param name="error">The failure description.</param>
    /// <returns>The failed result.</returns>
    public static TreasuryContinuousRateResult Failed(string error) => new(null, error);
}
