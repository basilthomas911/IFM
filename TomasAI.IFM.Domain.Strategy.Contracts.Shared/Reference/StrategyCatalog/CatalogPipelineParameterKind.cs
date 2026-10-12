using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid; preserve the published enum values.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum CatalogPipelineParameterKind : short { IntrinsicTimeStrategyWorkflow = 1, RegimeDiscovery = 2, MarketCondition = 3, TradeSelection = 4, OrderComposition = 5, RiskManagement = 6, MarketConditionAssessment = 7 }
