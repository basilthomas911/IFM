namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Defines the closed-observation history required to produce a fully comparable Wilder ATR signal.</summary>
public static class FuturesAtrHistoricalWarmupRequirement
{
    /// <summary>Gets the number of prior completed ATR values used by the volatility baseline.</summary>
    public const int BaselineLength = 20;

    /// <summary>
    /// Returns the minimum closed observations required to seed Wilder ATR and then populate its prior-only baseline.
    /// </summary>
    /// <param name="periodLength">The configured Wilder ATR period.</param>
    /// <returns>The required number of chronological, completed observations.</returns>
    public static int GetRequiredObservationCount(int periodLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodLength);
        return checked(periodLength + BaselineLength);
    }
}
