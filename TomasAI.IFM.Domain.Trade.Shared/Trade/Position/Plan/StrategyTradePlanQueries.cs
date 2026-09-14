using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>A bounded page of material strategy Trade Plan snapshots.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanHistoryPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState);

/// <summary>A bounded page of material Trade Plans across strategies for one value date.</summary>
[MessagePackObject]
public sealed record StrategyTradePlanActivityPage(
    [property: Key(0)] StrategyTradePlanSnapshot[] Items,
    [property: Key(1)] byte[]? PagingState);

/// <summary>Lists material Trade Plan activity for one bounded value-date partition.</summary>
[MessagePackObject]
public sealed record GetStrategyTradePlanActivityQuery : IQuery<StrategyTradePlanActivityPage>
{
    [IgnoreMember] public const string Actor = "StrategyTradePlanActivityQuery";
    [IgnoreMember] public const string Verb = "GetStrategyTradePlanActivity";
    [IgnoreMember] public const int ErrorId = 27127;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public DateOnly ValueDate { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads the current material Iron Condor Trade Plan.</summary>
[MessagePackObject]
public sealed record GetCurrentIronCondorTradePlanQuery : IQuery<StrategyTradePlanSnapshot?>
{
    [IgnoreMember] public const string Actor = "IronCondorTradePlanQuery";
    [IgnoreMember] public const string Verb = "GetCurrentIronCondorTradePlan";
    [IgnoreMember] public const int ErrorId = 27121;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public IronCondorTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads material Iron Condor Trade Plan history.</summary>
[MessagePackObject]
public sealed record GetIronCondorTradePlanHistoryQuery : IQuery<StrategyTradePlanHistoryPage>
{
    [IgnoreMember] public const string Actor = GetCurrentIronCondorTradePlanQuery.Actor;
    [IgnoreMember] public const string Verb = "GetIronCondorTradePlanHistory";
    [IgnoreMember] public const int ErrorId = 27122;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public IronCondorTradePlanId PlanId { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads the current material Vertical Spread Trade Plan.</summary>
[MessagePackObject]
public sealed record GetCurrentVerticalSpreadTradePlanQuery : IQuery<StrategyTradePlanSnapshot?>
{
    [IgnoreMember] public const string Actor = "VerticalSpreadTradePlanQuery";
    [IgnoreMember] public const string Verb = "GetCurrentVerticalSpreadTradePlan";
    [IgnoreMember] public const int ErrorId = 27123;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public VerticalSpreadTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads material Vertical Spread Trade Plan history.</summary>
[MessagePackObject]
public sealed record GetVerticalSpreadTradePlanHistoryQuery : IQuery<StrategyTradePlanHistoryPage>
{
    [IgnoreMember] public const string Actor = GetCurrentVerticalSpreadTradePlanQuery.Actor;
    [IgnoreMember] public const string Verb = "GetVerticalSpreadTradePlanHistory";
    [IgnoreMember] public const int ErrorId = 27124;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public VerticalSpreadTradePlanId PlanId { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads the current material outright Futures Trade Plan.</summary>
[MessagePackObject]
public sealed record GetCurrentFuturesTradePlanQuery : IQuery<StrategyTradePlanSnapshot?>
{
    [IgnoreMember] public const string Actor = "FuturesTradePlanQuery";
    [IgnoreMember] public const string Verb = "GetCurrentFuturesTradePlan";
    [IgnoreMember] public const int ErrorId = 27125;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public FuturesTradePlanId PlanId { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Reads material outright Futures Trade Plan history.</summary>
[MessagePackObject]
public sealed record GetFuturesTradePlanHistoryQuery : IQuery<StrategyTradePlanHistoryPage>
{
    [IgnoreMember] public const string Actor = GetCurrentFuturesTradePlanQuery.Actor;
    [IgnoreMember] public const string Verb = "GetFuturesTradePlanHistory";
    [IgnoreMember] public const int ErrorId = 27126;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public FuturesTradePlanId PlanId { get; init; }
    [Key(3)] public int PageSize { get; init; } = 100;
    [Key(4)] public byte[]? PagingState { get; init; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => null;
}

/// <summary>Client read surface for current and historical strategy Trade Plans.</summary>
public interface IStrategyTradePlanQueryApi
{
    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentIronCondorAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetIronCondorHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentVerticalSpreadAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetVerticalSpreadHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanSnapshot?>> GetCurrentFuturesAsync(
        StrategyPositionId positionId, DateOnly valueDate, CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanHistoryPage>> GetFuturesHistoryAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<StrategyTradePlanActivityPage>> GetActivityAsync(
        DateOnly valueDate, int pageSize = 100, byte[]? pagingState = null,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.ExitPositionWorkflowProjection?>>
        GetCurrentExitWorkflowAsync(
            StrategyPositionId positionId, DateOnly valueDate,
            CancellationToken cancellationToken = default);
    Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.PositionExitWorkflowHistoryPage>>
        GetExitWorkflowTimelineAsync(
            StrategyPositionId positionId, DateOnly valueDate, int pageSize = 100,
            byte[]? pagingState = null, CancellationToken cancellationToken = default);
}
