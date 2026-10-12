using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
public enum MarketDataOperationStage : byte
{
    MarketOutlookChannel,
    MarketOutlookComposition,
    MarketOutlookCache,
    MarketOutlookPublication,
    DatabentoNative,
    DatabentoInterop,
    DatabentoAggregation,
    DatabentoLifecycle,
    DatabentoRefresh,
    DatabentoWorkerProcess,
    DatabentoGenerationIngress,
    RsiAnalytics,
    TdiAnalytics,
    ItiAnalytics,
    EmaAnalytics,
    BollingerBandAnalytics,
    VixAnalytics,
    EodAnalytics,
    FuturesTradeSignal,
    UiDelivery,
    DatabentoRealtimePublication,
    VwapAnalytics,
    AdxAnalytics,
    AtrAnalytics,
    MacdAnalytics
}
