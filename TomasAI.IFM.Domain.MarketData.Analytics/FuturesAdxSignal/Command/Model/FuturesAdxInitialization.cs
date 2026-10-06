using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;

/// <summary>Contains a prevalidated ADX restoration or historical initialization plan.</summary>
public sealed record FuturesAdxInitialization(FuturesAdxSignalReadModel? RestoredSignal,
    bool ResetForHistoricalSeed, IReadOnlyList<FuturesAdxSignalGeneratedEvent> HistoricalSignals,
    string? RejectionReason)
{
    /// <summary>Gets whether all historical signals can be applied and the requested seed is warm.</summary>
    public bool Accepted => RejectionReason is null;
}
