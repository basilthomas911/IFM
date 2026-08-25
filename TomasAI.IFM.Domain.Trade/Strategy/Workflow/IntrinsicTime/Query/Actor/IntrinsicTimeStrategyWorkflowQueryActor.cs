using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;

/// <summary>Serves side-effect-free Intrinsic Time Strategy workflow projections from cache and ScyllaDB.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryActor(
    IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> actorContext)
    : BaseQueryActor<IntrinsicTimeStrategyWorkflowQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> Parsers =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetIntrinsicTimeStrategyWorkflowByIdQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowByIdQuery, IntrinsicTimeStrategyWorkflowReadModel>()!,
            [GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb] = message => message.AsQuery<GetActiveIntrinsicTimeStrategyWorkflowQuery, ActiveIntrinsicTimeStrategyWorkflowReadModel>()!,
            [GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery, IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>()!,
            [GetIntrinsicTimeStrategyWorkflowStageStateQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowStageStateQuery, StrategyWorkflowStageState>()!,
            [GetIntrinsicTimeStrategyWorkflowTimelineQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowTimelineQuery, IntrinsicTimeStrategyWorkflowTimelineReadModel[]>()!,
            [GetRecentIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetRecentIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!,
            [GetCompletedIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetCompletedIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!,
            [GetStoppedIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetStoppedIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!
        };

    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;

    IIntrinsicTimeStrategyWorkflowQueryContext ActorContext { get; } = RequireContext(actorContext);

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Query, Name: ActorName } ||
            !Parsers.TryGetValue(subject.Verb, out var parser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {subject}");

        var query = parser(message);
        context.SetMessageInfo(subject.ThreadId, subject.Verb, new ActorMessageInfo(message, query));
        return query;
    }

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        switch (query)
        {
            case GetIntrinsicTimeStrategyWorkflowByIdQuery byId:
            {
                var result = await ActorContext.DbFactory.TradeDb
                    .GetIntrinsicTimeStrategyWorkflowAsync(byId.WorkflowId, cancellationToken).ConfigureAwait(false);
                RequireRevision(result?.WorkflowRevision, byId.MinimumWorkflowRevision, byId.WorkflowId.ToString());
                await context.ReplyAsync(query.Subject.ThreadId, byId.Subject.Verb,
                    new ServiceResult<IntrinsicTimeStrategyWorkflowReadModel>(result!)).ConfigureAwait(false);
                break;
            }
            case GetActiveIntrinsicTimeStrategyWorkflowQuery active:
            {
                ActiveIntrinsicTimeStrategyWorkflowReadModel? result;
                if (!ActorContext.ProjectionCache.TryGet(active.WorkflowEntityId, out result))
                {
                    result = await ActorContext.DbFactory.TradeDb
                        .GetActiveIntrinsicTimeStrategyWorkflowAsync(active.WorkflowEntityId, cancellationToken)
                        .ConfigureAwait(false);
                    if (result is not null)
                        ActorContext.ProjectionCache.Set(result);
                }
                RequireRevision(result?.WorkflowRevision, active.MinimumWorkflowRevision, active.WorkflowEntityId);
                await context.ReplyAsync(query.Subject.ThreadId, active.Subject.Verb,
                    new ServiceResult<ActiveIntrinsicTimeStrategyWorkflowReadModel>(result!)).ConfigureAwait(false);
                break;
            }
            case GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery attempts:
            {
                var result = await ActorContext.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowStartAttemptsAsync(
                    attempts.WorkflowEntityId, attempts.BeforeUtc, RequirePageSize(attempts.PageSize), cancellationToken)
                    .ConfigureAwait(false);
                await ReplyArray(context, query, attempts.Subject.Verb, result).ConfigureAwait(false);
                break;
            }
            case GetIntrinsicTimeStrategyWorkflowStageStateQuery stage:
            {
                var projection = await ActorContext.DbFactory.TradeDb
                    .GetIntrinsicTimeStrategyWorkflowAsync(stage.WorkflowId, cancellationToken).ConfigureAwait(false);
                RequireRevision(projection?.WorkflowRevision, stage.MinimumWorkflowRevision, stage.WorkflowId.ToString());
                if (projection is null)
                    throw new KeyNotFoundException($"Workflow {stage.WorkflowId} was not found.");
                var state = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowState>(projection.StatePayload);
                var result = stage.Stage switch
                {
                    StrategyWorkflowStage.RegimeDiscovery => state.RegimeDiscovery,
                    StrategyWorkflowStage.MarketCondition => state.MarketCondition,
                    StrategyWorkflowStage.TradeSelection => state.TradeSelection,
                    StrategyWorkflowStage.OrderComposition => state.OrderComposition,
                    StrategyWorkflowStage.RiskManagement => state.RiskManagement,
                    _ => throw new ArgumentOutOfRangeException(nameof(stage.Stage), stage.Stage, "A concrete stage is required.")
                };
                await context.ReplyAsync(query.Subject.ThreadId, stage.Subject.Verb,
                    new ServiceResult<StrategyWorkflowStageState>(result)).ConfigureAwait(false);
                break;
            }
            case GetIntrinsicTimeStrategyWorkflowTimelineQuery timeline:
            {
                var result = await ActorContext.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowTimelineAsync(
                    timeline.WorkflowId, timeline.AfterEventId, RequirePageSize(timeline.PageSize), cancellationToken)
                    .ConfigureAwait(false);
                await ReplyArray(context, query, timeline.Subject.Verb, result).ConfigureAwait(false);
                break;
            }
            case GetRecentIntrinsicTimeStrategyWorkflowsQuery recent:
            {
                var result = await ActorContext.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByEntityAsync(
                    recent.WorkflowEntityId, recent.BeforeUtc, RequirePageSize(recent.PageSize), cancellationToken)
                    .ConfigureAwait(false);
                await ReplyArray(context, query, recent.Subject.Verb, result).ConfigureAwait(false);
                break;
            }
            case GetCompletedIntrinsicTimeStrategyWorkflowsQuery completed:
            {
                var result = await ActorContext.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
                    StrategyWorkflowStatus.Completed, completed.StartDate, completed.EndDate,
                    RequirePageSize(completed.PageSize), cancellationToken).ConfigureAwait(false);
                await ReplyArray(context, query, completed.Subject.Verb, result).ConfigureAwait(false);
                break;
            }
            case GetStoppedIntrinsicTimeStrategyWorkflowsQuery stopped:
            {
                var result = await ActorContext.DbFactory.TradeDb.GetIntrinsicTimeStrategyWorkflowsByStatusAsync(
                    StrategyWorkflowStatus.Stopped, stopped.StartDate, stopped.EndDate,
                    RequirePageSize(stopped.PageSize), cancellationToken).ConfigureAwait(false);
                await ReplyArray(context, query, stopped.Subject.Verb, result).ConfigureAwait(false);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported workflow query: {query.GetType().Name}");
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
    {
        var errorCode = exception is ProjectionSnapshotNotReadyException ? 25009 : query?.ErrorCode ?? 25000;
        try
        {
            await context.ReplyAsync(threadId, verb,
                new ServiceFailed<ActorEntityId>(errorCode, exception.Message)).ConfigureAwait(false);
        }
        catch (Exception replyException)
        {
            ActorContext.Logger.LogError(replyException,
                "Failed to return workflow query error for {ThreadId}", threadId);
        }
    }

    static async ValueTask ReplyArray<T>(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IQuery query,
        string verb,
        ICollection<T> values)
        where T : class
        => await context.ReplyAsync(query.Subject.ThreadId, verb,
            new ServiceResult<T[]>(values.ToArray())).ConfigureAwait(false);

    static int RequirePageSize(int pageSize)
    {
        if (pageSize is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, "Page size must be between 1 and 1000.");
        return pageSize;
    }

    static void RequireRevision(long? actualRevision, long minimumRevision, string identity)
    {
        if (minimumRevision > 0 && (!actualRevision.HasValue || actualRevision.Value < minimumRevision))
            throw new ProjectionSnapshotNotReadyException(identity, minimumRevision, actualRevision);
    }

    static IIntrinsicTimeStrategyWorkflowQueryContext RequireContext(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context)
        => context as IIntrinsicTimeStrategyWorkflowQueryContext
            ?? throw new ArgumentException(
                $"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowQueryContext)}.",
                nameof(context));
}

/// <summary>Signals that a projection exists behind the caller's required workflow revision.</summary>
public sealed class ProjectionSnapshotNotReadyException : Exception
{
    /// <summary>Initializes a minimum-revision projection error.</summary>
    public ProjectionSnapshotNotReadyException(string identity, long minimumRevision, long? actualRevision)
        : base($"SnapshotNotReady: {identity} requires revision {minimumRevision}; current revision is {actualRevision?.ToString() ?? "missing"}.")
    {
    }
}
