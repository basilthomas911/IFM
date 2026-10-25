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

/// <summary>Portfolio-neutral strategy catalog universe frozen for one workflow execution.</summary>
[MessagePackObject]
public sealed record StrategySelectionUniverse
{
    [Key(0)] public short SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid WorkflowId { get; init; }
    [Key(2)] public long WorkflowRevision { get; init; }
    [Key(3)] public Guid CorrelationId { get; init; }
    [Key(4)] public string InstrumentRoot { get; init; } = string.Empty;
    [Key(5)] public TimeFrameType TargetHorizon { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public CatalogKey[] DeploymentKeys { get; init; } = [];
    [Key(7)] public DateTime FrozenAtUtc { get; init; }
    [Key(8)] public DateTime ValidUntilUtc { get; init; }
    [Key(9)] public string PayloadSha256 { get; init; } = string.Empty;
}
