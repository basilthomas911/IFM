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
public sealed record TradeSelectionBinding
{
    [Key(0)] public short SchemaVersion { get; init; }
    PortfolioFundStrategySnapshot _portfolioSnapshot = new();
    [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
    [Newtonsoft.Json.JsonConverter(typeof(SelectionPortfolioSnapshotJsonConverter))]
    [Key(1)] public PortfolioFundStrategySnapshot PortfolioSnapshot { get => _portfolioSnapshot.DefensiveCopy(); init => _portfolioSnapshot = value?.DefensiveCopy() ?? new(); }
    SelectionCatalogDefinitionSnapshot[] _CatalogDefinitions = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(2)] public SelectionCatalogDefinitionSnapshot[] CatalogDefinitions { get => [.. _CatalogDefinitions]; init => _CatalogDefinitions = value is null ? [] : [.. value]; }
    SelectionDeploymentSnapshot[] _DeploymentSnapshots = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(3)] public SelectionDeploymentSnapshot[] DeploymentSnapshots { get => [.. _DeploymentSnapshots]; init => _DeploymentSnapshots = value is null ? [] : [.. value]; }
    SelectionPipelinePolicySnapshot[] _PipelinePolicies = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(4)] public SelectionPipelinePolicySnapshot[] PipelinePolicies { get => [.. _PipelinePolicies]; init => _PipelinePolicies = value is null ? [] : [.. value]; }
    SelectionCandidateBinding[] _Candidates = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(5)] public SelectionCandidateBinding[] Candidates { get => [.. _Candidates]; init => _Candidates = value is null ? [] : [.. value]; }
    SelectionAssignmentExclusion[] _ExcludedAssignments = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(6)] public SelectionAssignmentExclusion[] ExcludedAssignments { get => [.. _ExcludedAssignments]; init => _ExcludedAssignments = value is null ? [] : [.. value]; }
    [Key(7)] public SelectionPipelinePolicyReference CommonPolicy { get; init; }
    [Key(8)] public DateTime FrozenAtUtc { get; init; }
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public DateOnly RequestedTradeDate { get; init; }
    [Key(11)] public string TradeDatePolicy { get; init; } = string.Empty;
    [Key(12)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(13)] public StrategySelectionUniverse? StrategyUniverse { get; init; }
    /// <summary>Append-only Stage 4 dependency and exact point-in-time snapshot input.</summary>
    [Key(14)] public VolatilityWorkflowInput? VolatilityInput { get; init; }
}
