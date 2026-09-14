namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;

/// <summary>Defines editable trading-day defaults for Futures ITI timeframes.</summary>
public sealed record FuturesItiSignalDefaultTradingDays
{
    /// <summary>Gets the number of trading days represented by the Daily timeframe.</summary>
    public int Daily { get; init; } = 1;

    /// <summary>Gets the number of trading days represented by the Weekly timeframe.</summary>
    public int Weekly { get; init; } = 10;

    /// <summary>Gets the number of trading days represented by the Monthly timeframe.</summary>
    public int Monthly { get; init; } = 30;
}

/// <summary>Defines the versioned Futures ITI parameter payload.</summary>
public sealed record FuturesItiSignalParameterSet
{
    /// <summary>Gets the immutable parameter-set identity.</summary>
    public Guid ParameterSetId { get; init; }

    /// <summary>Gets the immutable version allocated when the draft is saved.</summary>
    public int Version { get; init; }

    /// <summary>Gets the payload schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Gets the trading-day defaults used by each supported ITI timeframe.</summary>
    public FuturesItiSignalDefaultTradingDays DefaultTradingDays { get; init; } = new();
}
