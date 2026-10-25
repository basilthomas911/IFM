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
public sealed record SelectionCandidateIntent
{
    [Key(0)] public string CandidateHash { get; init; } = string.Empty;
    [Key(1)] public long AssignmentVersion { get; init; }
    [Key(2)] public CatalogKey DeploymentKey { get; init; }
    [Key(3)] public CatalogKey StrategyKey { get; init; }
    [Key(4)] public CatalogKey StructureKey { get; init; }
    [Key(5)] public CatalogKey VariantKey { get; init; }
    [Key(6)] public SelectionProduct Product { get; init; }
    [Key(7)] public string Side { get; init; } = string.Empty;
    [Key(8)] public string Bias { get; init; } = string.Empty;
    [Key(9)] public string PremiumMode { get; init; } = string.Empty;
    [Key(10)] public SelectionPipelinePolicyReference SelectionPolicyReference { get; init; }
    [Key(11)] public SelectionPipelinePolicyReference CompositionPolicyReference { get; init; }
    SelectionParameterBinding[] _SpecializedParameterBindings = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(12)] public SelectionParameterBinding[] SpecializedParameterBindings { get => [.. _SpecializedParameterBindings]; init => _SpecializedParameterBindings = value is null ? [] : [.. value]; }
    CatalogKey[] _FamilyKeys = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(13)] public CatalogKey[] FamilyKeys { get => [.. _FamilyKeys]; init => _FamilyKeys = value is null ? [] : [.. value]; }
}
