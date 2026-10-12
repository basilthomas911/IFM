using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoStageMetric(long Requested, long Started, long Completed, long Failed, DateTime? LastActivityUtc);
