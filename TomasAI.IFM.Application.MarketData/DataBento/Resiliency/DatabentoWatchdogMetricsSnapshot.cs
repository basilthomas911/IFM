using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;
public sealed record DatabentoWatchdogMetricsSnapshot(IReadOnlyDictionary<MarketDataOperationStage, DatabentoStageMetric> Stages);
