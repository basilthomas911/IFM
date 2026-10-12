using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>Contains one Wilder ATR transition and its complete replay checkpoint.</summary>
public sealed record FuturesAtrWilderResult(
    decimal TrueRange,
    decimal? AtrValue,
    decimal? PreviousAtrValue,
    decimal? AtrBaseline,
    decimal? AtrRatio,
    bool IsWarm,
    FuturesAtrAccumulatorCheckpoint Checkpoint);
