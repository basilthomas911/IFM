using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetLatestMarketConditionQuery : IQuery<MarketConditionReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetLatestMarketConditionQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="fundId">The FundId field.</param>
    /// <param name="instrumentRoot">The InstrumentRoot field.</param>
    /// <param name="targetHorizon">The TargetHorizon field.</param>
    [SerializationConstructor]
    public GetLatestMarketConditionQuery(ActorSubject subject, IActorEntityId entityId, int fundId, string instrumentRoot, TimeFrameType targetHorizon)
    {
        Subject = subject;
        EntityId = entityId;
        FundId = fundId;
        InstrumentRoot = instrumentRoot;
        TargetHorizon = targetHorizon;
    }
    [IgnoreMember] public const string Actor = GetMarketConditionQuery.Actor;
    [IgnoreMember] public const string Verb = "GetLatest";
    [IgnoreMember] public const int ErrorId = 23203;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public string InstrumentRoot { get; init; } = "ES";
    [Key(4)] public TimeFrameType TargetHorizon { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
