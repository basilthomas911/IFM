using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;

/// <summary>Contains one VWAP state transition and its projected signal.</summary>
public sealed record FuturesVwapAccumulatorResult(
    FuturesVwapCheckpoint FuturesVwapCheckpoint,
    FuturesVwapSignalReadModel FuturesVwapSignal,
    bool Changed);
