using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;

/// <summary>Contains one EMA state transition.</summary>
public sealed record FuturesEmaAccumulatorResult(
    FuturesEmaAccumulatorCheckpoint FuturesEmaCheckpoint,
    FuturesEmaSignalReadModel? FuturesEmaSignal,
    MarketObservationApplicationDisposition Disposition)
{
    /// <summary>Gets whether this transition advanced durable state.</summary>
    public bool IsApplied => Disposition == MarketObservationApplicationDisposition.Applied;
}
