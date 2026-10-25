using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;

/// <summary>Represents immutable event-sourced state for Bollinger calculation.</summary>
[MessagePackObject]
public sealed record FuturesBbAccumulatorCheckpoint
{
    /// <summary>Gets at most the last 20 closes.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(0)] public decimal[] Closes { get; init; } = [];
    /// <summary>Gets at most the prior 20 completed positive BB20 widths.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public model contract.")]
    [Key(1)] public decimal[] CompletedWidths20 { get; init; } = [];
    /// <summary>Gets the last observation identity.</summary>
    [Key(2)] public FuturesTradeSessionBarId LastObservationId { get; init; }
    /// <summary>Gets the last source sequence.</summary>
    [Key(3)] public long LastSourceSequence { get; init; }
    /// <summary>Gets the exclusive end of the last accepted observation interval.</summary>
    [Key(4)] public DateTimeOffset LastIntervalEndUtc { get; init; }
    /// <summary>Gets the source stream epoch of the last accepted observation.</summary>
    [Key(5)] public Guid LastStreamEpochId { get; init; }
}
