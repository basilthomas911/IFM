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
public sealed record SelectionDeploymentSnapshot
{
    [Key(0)] public CatalogKey DeploymentKey { get; init; }
    [Key(1)] public DateTime AsOfUtc { get; init; }
    CatalogKey[] _DefinitionKeys = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(2)] public CatalogKey[] DefinitionKeys { get => [.. _DefinitionKeys]; init => _DefinitionKeys = value is null ? [] : [.. value]; }
    [Key(3)] public string ContentHash { get; init; } = string.Empty;
}
