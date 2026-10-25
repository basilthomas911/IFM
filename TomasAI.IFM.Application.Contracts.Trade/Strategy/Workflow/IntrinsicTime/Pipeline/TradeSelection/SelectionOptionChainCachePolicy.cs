using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

/// <summary>Pinned global cache policy from the accepted construction policy. Hash is the normalized cache parameter payload hash.</summary>
public sealed record SelectionOptionChainCachePolicy
{
    [JsonRequired] public Guid ParameterSetId { get; init; }
    [JsonRequired] public int Version { get; init; }
    [JsonRequired] public string ConfigurationDigest { get; init; } = "";
    [JsonRequired] public int MaximumQuoteAgeMilliseconds { get; init; } = 1000;
    [JsonRequired] public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    /// <summary>Checks identity, hash syntax and explicit quality bounds without loading configuration.</summary>
    public bool IsValid() => ParameterSetId != Guid.Empty && Version > 0 && ConfigurationDigest.Length == 64
        && ConfigurationDigest.All(Uri.IsHexDigit) && MaximumQuoteAgeMilliseconds is >= 1 and <= 5000
        && MaximumQuoteSkewMilliseconds is >= 0 and <= 2000;
}
