using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

/// <summary>Contains the complete, prevalidated RSI startup and TDI-window publication plan.</summary>
#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record FuturesRsiInitialization(FuturesRsiSignalReadModel? RestoredSignal,
    FuturesRsiAccumulatorCheckpoint? RestoredCheckpoint, bool ResetForHistoricalSeed,
    int HistoricalSeedCount, string HistoricalSeedReason, FuturesRsiSignalReadModel[] HistoricalWarmSignals,
    FuturesRsiSignalReadModel? FuturesRsiSignal, FuturesRsiAccumulatorCheckpoint? FuturesRsiCheckpoint,
    FuturesRsiSignalReadModel[]? FuturesRsiSignals, string? RejectionReason)
{
    /// <summary>Gets whether the seed and any requested warm TDI window are valid before state application.</summary>
    public bool Accepted => RejectionReason is null;
}

#pragma warning restore CA1819
