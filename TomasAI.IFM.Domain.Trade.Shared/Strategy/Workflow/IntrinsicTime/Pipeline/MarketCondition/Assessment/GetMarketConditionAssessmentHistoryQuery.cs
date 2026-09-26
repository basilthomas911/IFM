using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
[MessagePackObject(AllowPrivate = true)]
public sealed record GetMarketConditionAssessmentHistoryQuery : IQuery<MarketConditionAssessmentCompletedEvent[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetMarketConditionAssessmentHistoryQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="marketProfileId">The MarketProfileId field.</param>
    /// <param name="instrumentRoot">The InstrumentRoot field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    /// <param name="beforeUtc">The BeforeUtc field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    [SerializationConstructor]
    public GetMarketConditionAssessmentHistoryQuery(ActorSubject subject, IActorEntityId entityId, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize)
    {
        Subject = subject;
        EntityId = entityId;
        MarketProfileId = marketProfileId;
        InstrumentRoot = instrumentRoot;
        TargetHorizon = targetHorizon;
        BeforeUtc = beforeUtc;
        PageSize = pageSize;
    }
    public const string Actor = GetMarketConditionAssessmentQuery.Actor;
    public const string Verb = "GetAssessmentHistory";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public string MarketProfileId { get; init; } = "";
    [Key(3)] public string InstrumentRoot { get; init; } = "ES";
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [Key(5)] public DateTime BeforeUtc { get; init; } = DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc);
    [Key(6)] public int PageSize { get; init; } = 25;
    [IgnoreMember] public int ErrorCode => 23221;
    [IgnoreMember] public string? QueryParams { get; init; }
}
