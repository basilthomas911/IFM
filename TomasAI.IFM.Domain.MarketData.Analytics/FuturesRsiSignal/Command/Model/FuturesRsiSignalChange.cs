using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

/// <summary>Contains a computed RSI signal, checkpoint, and optional complete RSI publication window.</summary>
public sealed record FuturesRsiSignalChange(FuturesRsiSignalReadModel? FuturesRsiSignal,
    FuturesRsiAccumulatorCheckpoint? FuturesRsiCheckpoint,
    IReadOnlyCollection<FuturesRsiSignalReadModel>? FuturesRsiSignals, bool HasChanged);
