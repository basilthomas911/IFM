using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record AssessmentEvidence(
    [property: Key(0)] TimeFrameType Horizon,
    [property: Key(1)] string SourceId,
    [property: Key(2)] string Feature,
    [property: Key(3)] decimal? Value,
    [property: Key(4)] string Unit,
    [property: Key(5)] DateTime ObservedAtUtc,
    [property: Key(6)] decimal AgeSeconds,
    [property: Key(7)] MarketSourceAvailability Availability,
    [property: Key(8)] string Reason,
    [property: Key(9)] long Sequence = 0);
