using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;

/// <summary>Gets one workflow execution by its immutable workflow identity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowByIdQuery : IQuery<IntrinsicTimeStrategyWorkflowReadModel>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = "IntrinsicTimeStrategyWorkflowQuery";
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetById";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25001;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the requested workflow identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the minimum acceptable projection revision.</summary>
    [Key(3)] public long MinimumWorkflowRevision { get; init; }
}

/// <summary>Gets the active workflow for one stable workflow entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetActiveIntrinsicTimeStrategyWorkflowQuery : IQuery<ActiveIntrinsicTimeStrategyWorkflowReadModel>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetActive";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25002;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the formatted stable workflow entity identity.</summary>
    [Key(2)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the minimum acceptable projection revision.</summary>
    [Key(3)] public long MinimumWorkflowRevision { get; init; }
}

/// <summary>Gets accepted and rejected workflow start attempts for an entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery : IQuery<IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetStartAttempts";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25003;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the formatted workflow entity identity.</summary>
    [Key(2)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the exclusive UTC page cursor.</summary>
    [Key(3)] public DateTime BeforeUtc { get; init; } = DateTime.MaxValue;
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}

/// <summary>Gets one immutable stage state from a workflow projection snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowStageStateQuery : IQuery<StrategyWorkflowStageState>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetStageState";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25004;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the requested workflow identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the requested stage.</summary>
    [Key(3)] public StrategyWorkflowStage Stage { get; init; }
    /// <summary>Gets the minimum acceptable projection revision.</summary>
    [Key(4)] public long MinimumWorkflowRevision { get; init; }
}

/// <summary>Gets an event-ordered workflow timeline page.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetIntrinsicTimeStrategyWorkflowTimelineQuery : IQuery<IntrinsicTimeStrategyWorkflowTimelineReadModel[]>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetTimeline";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25005;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the requested workflow identity.</summary>
    [Key(2)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the exclusive event-id page cursor.</summary>
    [Key(3)] public long AfterEventId { get; init; }
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}

/// <summary>Gets recent workflow executions for one entity.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetRecentIntrinsicTimeStrategyWorkflowsQuery : IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetRecent";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25006;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the formatted workflow entity identity.</summary>
    [Key(2)] public string WorkflowEntityId { get; init; } = string.Empty;
    /// <summary>Gets the exclusive UTC page cursor.</summary>
    [Key(3)] public DateTime BeforeUtc { get; init; } = DateTime.MaxValue;
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}

/// <summary>Gets successfully completed workflows for a bounded date range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetCompletedIntrinsicTimeStrategyWorkflowsQuery : IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetCompleted";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25007;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the inclusive start date.</summary>
    [Key(2)] public DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive end date.</summary>
    [Key(3)] public DateOnly EndDate { get; init; }
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}

/// <summary>Gets terminally stopped workflows for a bounded date range.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetStoppedIntrinsicTimeStrategyWorkflowsQuery : IQuery<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>
{
    /// <summary>Query actor name.</summary>
    [IgnoreMember] public const string Actor = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    /// <summary>Query verb.</summary>
    [IgnoreMember] public const string Verb = "GetStopped";
    /// <summary>Stable query error code.</summary>
    [IgnoreMember] public const int ErrorId = 25008;
    /// <inheritdoc />
    [Key(0)] public ActorSubject Subject { get; init; }
    /// <inheritdoc />
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    /// <inheritdoc />
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    /// <inheritdoc />
    [IgnoreMember] public string? QueryParams { get; init; }
    /// <summary>Gets the inclusive start date.</summary>
    [Key(2)] public DateOnly StartDate { get; init; }
    /// <summary>Gets the inclusive end date.</summary>
    [Key(3)] public DateOnly EndDate { get; init; }
    /// <summary>Gets the maximum returned item count.</summary>
    [Key(4)] public int PageSize { get; init; } = 100;
}
