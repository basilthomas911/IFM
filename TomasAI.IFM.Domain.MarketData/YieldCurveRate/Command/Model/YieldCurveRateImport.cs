using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.Model;

/// <summary>Computed parameters for a durable YieldCurveRate import request; contains no mutable actor state.</summary>
internal sealed record YieldCurveRateImport(DateTime ImportDate, ImportDuplicatePolicy DuplicatePolicy);
