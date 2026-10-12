using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

/// <summary>Immutable mapping from an exact catalog structure version to its global option-chain policy.</summary>
public sealed record SelectionStructureOptionChainCachePolicy
{
    [JsonRequired] public Guid StructureId { get; init; }
    [JsonRequired] public int StructureVersion { get; init; }
    [JsonRequired] public SelectionOptionChainCachePolicy Policy { get; init; } = new();
}
