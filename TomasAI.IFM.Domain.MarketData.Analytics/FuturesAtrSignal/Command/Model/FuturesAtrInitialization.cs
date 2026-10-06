using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>Contains a prevalidated ATR restoration or historical initialization plan.</summary>
public sealed record FuturesAtrInitialization(FuturesAtrSignalReadModel? RestoredSignal,
    bool ResetForHistoricalSeed, IReadOnlyList<FuturesAtrSignalGeneratedEvent> HistoricalSignals, string? RejectionReason)
{
    /// <summary>Gets whether the complete historical initialization passed its domain guards.</summary>
    public bool Accepted => RejectionReason is null;
}
