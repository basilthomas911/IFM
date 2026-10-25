namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;

/// <summary>Defines the versioned Futures ITI parameter payload.</summary>
[MessagePack.MessagePackObject]
public sealed record FuturesItiSignalParameterSet
{
    /// <summary>Gets the immutable parameter-set identity.</summary>
    [MessagePack.Key(0)] public Guid ParameterSetId { get; init; }

    /// <summary>Gets the immutable version allocated when the draft is saved.</summary>
    [MessagePack.Key(1)] public int Version { get; init; }

    /// <summary>Gets the payload schema version.</summary>
    [MessagePack.Key(2)] public int SchemaVersion { get; init; } = 1;

    /// <summary>Gets the trading-day defaults used by each supported ITI timeframe.</summary>
    [MessagePack.Key(3)] public FuturesItiSignalDefaultTradingDays DefaultTradingDays { get; init; } = new();
}
