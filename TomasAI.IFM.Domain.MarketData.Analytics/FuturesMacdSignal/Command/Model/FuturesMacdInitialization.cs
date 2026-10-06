using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;

/// <summary>Contains a prevalidated MACD restoration or historical initialization plan.</summary>
public sealed record FuturesMacdInitialization(FuturesMacdSignalReadModel? RestoredSignal,
    bool ResetForHistoricalSeed, IReadOnlyList<FuturesMacdSignalGeneratedEvent> HistoricalSignals, string? RejectionReason)
{
    /// <summary>Gets whether the complete historical initialization passed its domain guards.</summary>
    public bool Accepted => RejectionReason is null;
}
