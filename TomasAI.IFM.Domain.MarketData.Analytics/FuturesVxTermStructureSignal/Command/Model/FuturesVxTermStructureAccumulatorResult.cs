using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Model;

/// <summary>Contains one accepted VX state transition and optional paired signal.</summary>
public sealed record FuturesVxTermStructureAccumulatorResult(
    FuturesVxTermStructureCheckpoint FuturesVxTermStructureCheckpoint,
    FuturesVxTermStructureSignalReadModel? FuturesVxTermStructureSignal,
    bool Changed = true);
