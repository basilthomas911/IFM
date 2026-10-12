using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

[MessagePackObject]
public sealed record MarketConditionEventRiskState
{
    [Key(0)] public MarketEventRiskStatus Status { get; init; }
    [Key(1)] public string EventId { get; init; } = string.Empty;
    [Key(2)] public string Category { get; init; } = string.Empty;
    [Key(3)] public MarketSourceObservation Observation { get; init; } = new();
    // Omit the absent appended field from JSON so legacy snapshot hashes remain unchanged.
    [Key(4)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public MarketConditionCalendarDownloadEvidence? DownloadEvidence { get; init; }
}
