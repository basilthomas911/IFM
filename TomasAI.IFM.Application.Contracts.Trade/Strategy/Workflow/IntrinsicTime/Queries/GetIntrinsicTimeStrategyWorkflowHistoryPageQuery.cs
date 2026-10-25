using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

/// <summary>Gets a page of workflows for one symbol, timeframe, and exact UTC range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowHistoryPageQuery
    : IQuery<IntrinsicTimeStrategyWorkflowHistoryPageReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyWorkflowHistoryPageQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="symbol">The Symbol field.</param>
    /// <param name="timePeriod">The TimePeriod field.</param>
    /// <param name="fromUtc">The FromUtc field.</param>
    /// <param name="toUtc">The ToUtc field.</param>
    /// <param name="pageNumber">The PageNumber field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    [SerializationConstructor]
    public GetIntrinsicTimeStrategyWorkflowHistoryPageQuery(ActorSubject subject, IActorEntityId entityId, string symbol, TimeFrameType timePeriod, DateTime fromUtc, DateTime toUtc, int pageNumber, int pageSize)
    {
        Subject = subject;
        EntityId = entityId;
        Symbol = symbol;
        TimePeriod = timePeriod;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    [IgnoreMember] public const string Verb = "GetHistoryPage";
    [IgnoreMember] public const int ErrorId = 25011;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public string Symbol { get; init; } = string.Empty;
    [Key(3)] public TimeFrameType TimePeriod { get; init; }
    [Key(4)] public DateTime FromUtc { get; init; }
    [Key(5)] public DateTime ToUtc { get; init; }
    [Key(6)] public int PageNumber { get; init; } = 1;
    [Key(7)] public int PageSize { get; init; } = 50;
}
