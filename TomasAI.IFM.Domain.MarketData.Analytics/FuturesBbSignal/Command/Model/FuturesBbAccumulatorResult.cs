using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;

/// <summary>Contains one Bollinger state transition.</summary>
public sealed record FuturesBbAccumulatorResult(
    FuturesBbAccumulatorCheckpoint FuturesBbCheckpoint,
    FuturesBbSignalReadModel? FuturesBbSignal,
    MarketObservationApplicationDisposition Disposition)
{
    /// <summary>Gets whether this transition advanced durable state.</summary>
    public bool IsApplied => Disposition == MarketObservationApplicationDisposition.Applied;
}
