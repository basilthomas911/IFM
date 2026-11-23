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

/// <summary>Gets a page of compact Strategy Updates grid rows for one symbol, timeframe, and exact UTC range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyUpdatesGridQuery
    : IQuery<IntrinsicTimeStrategyUpdatesGridPage>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetIntrinsicTimeStrategyUpdatesGridQuery() { }

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
    public GetIntrinsicTimeStrategyUpdatesGridQuery(ActorSubject subject, IActorEntityId entityId, string symbol, TimeFrameType timePeriod, DateTime fromUtc, DateTime toUtc, int pageNumber, int pageSize, string? contractId = null, string? assignmentKey = null, bool queueCatalog = false, DateTime? beforeSignalAtUtc = null, long beforeSignalSequence = 0, Guid beforeWorkflowId = default)
    {
        Subject = subject;
        EntityId = entityId;
        Symbol = symbol;
        TimePeriod = timePeriod;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        PageNumber = pageNumber;
        PageSize = pageSize;
        ContractId = contractId;
        AssignmentKey = assignmentKey;
        QueueCatalog = queueCatalog;
        BeforeSignalAtUtc = beforeSignalAtUtc;
        BeforeSignalSequence = beforeSignalSequence;
        BeforeWorkflowId = beforeWorkflowId;
    }
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    [IgnoreMember] public const string Verb = "GetStrategyUpdatesGrid";
    [IgnoreMember] public const int ErrorId = 25013;
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
    [Key(8)] public string? ContractId { get; init; }
    [Key(9)] public string? AssignmentKey { get; init; }
    [Key(10)] public bool QueueCatalog { get; init; }
    [Key(11)] public DateTime? BeforeSignalAtUtc { get; init; }
    [Key(12)] public long BeforeSignalSequence { get; init; }
    [Key(13)] public Guid BeforeWorkflowId { get; init; }
}
