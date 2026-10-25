using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

[MessagePackObject]
public sealed record TradeSelectionDecisionContext
{
    [Key(0)] public short SchemaVersion { get; init; }
    [Key(1)] public StrategyStageResultEnvelope RegimeResultEnvelope { get; init; }
    [Key(2)] public StrategyStageResultEnvelope AssessmentResultEnvelope { get; init; }
    [Key(3)] public TradeSelectionBinding SelectionBinding { get; init; }
    /// <summary>Exact accepted/unavailable input copied from the frozen binding.</summary>
    [Key(4)] public VolatilityWorkflowInput? VolatilityInput { get; init; }
}
