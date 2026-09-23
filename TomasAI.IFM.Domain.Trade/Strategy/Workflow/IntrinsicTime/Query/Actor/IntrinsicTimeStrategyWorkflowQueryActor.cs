using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;

/// <summary>Serves side-effect-free Intrinsic Time Strategy workflow projections from cache and ScyllaDB.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryActor(
    IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> actorContext)
    : BaseQueryActor<IntrinsicTimeStrategyWorkflowQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetIntrinsicTimeStrategyWorkflowByIdQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowByIdQuery, IntrinsicTimeStrategyWorkflowReadModel>()!,
            [GetIntrinsicTimeStrategyWorkflowsByIdsQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowsByIdsQuery, IntrinsicTimeStrategyWorkflowReadModel[]>()!,
            [GetActiveIntrinsicTimeStrategyWorkflowQuery.Verb] = message => message.AsQuery<GetActiveIntrinsicTimeStrategyWorkflowQuery, ActiveIntrinsicTimeStrategyWorkflowReadModel>()!,
            [GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery, IntrinsicTimeStrategyWorkflowStartAttemptReadModel[]>()!,
            [GetIntrinsicTimeStrategyWorkflowStageStateQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowStageStateQuery, StrategyWorkflowStageState>()!,
            [GetIntrinsicTimeStrategyWorkflowTimelineQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowTimelineQuery, IntrinsicTimeStrategyWorkflowTimelineReadModel[]>()!,
            [GetRecentIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetRecentIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!,
            [GetCompletedIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetCompletedIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!,
            [GetStoppedIntrinsicTimeStrategyWorkflowsQuery.Verb] = message => message.AsQuery<GetStoppedIntrinsicTimeStrategyWorkflowsQuery, IntrinsicTimeStrategyWorkflowHistoryReadModel[]>()!,
            [GetIntrinsicTimeStrategyWorkflowObservationQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowObservationQuery, IntrinsicTimeStrategyWorkflowObservationReadModel>()!,
            [GetIntrinsicTimeStrategyWorkflowHistoryPageQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowHistoryPageQuery, IntrinsicTimeStrategyWorkflowHistoryPageReadModel>()!
        };

    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;

    IIntrinsicTimeStrategyWorkflowQueryContext ActorContext { get; } = RequireContext(actorContext);

    /// <inheritdoc />
    protected override IQuery ParseMessage(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

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
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(ActorContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<IIntrinsicTimeStrategyWorkflowQueryContext, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IIntrinsicTimeStrategyWorkflowQueryContext, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetIntrinsicTimeStrategyWorkflowByIdQuery)] = static (services, context, query, cancellationToken) =>
            ((GetIntrinsicTimeStrategyWorkflowByIdQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowsByIdsQuery)] = static (services, context, query, cancellationToken) =>
            IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(
                services, context, (GetIntrinsicTimeStrategyWorkflowsByIdsQuery)query, cancellationToken),
        [typeof(GetActiveIntrinsicTimeStrategyWorkflowQuery)] = static (services, context, query, cancellationToken) =>
            ((GetActiveIntrinsicTimeStrategyWorkflowQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery)] = static (services, context, query, cancellationToken) =>
            ((GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowStageStateQuery)] = static (services, context, query, cancellationToken) =>
            ((GetIntrinsicTimeStrategyWorkflowStageStateQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowTimelineQuery)] = static (services, context, query, cancellationToken) =>
            ((GetIntrinsicTimeStrategyWorkflowTimelineQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetRecentIntrinsicTimeStrategyWorkflowsQuery)] = static (services, context, query, cancellationToken) =>
            ((GetRecentIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetCompletedIntrinsicTimeStrategyWorkflowsQuery)] = static (services, context, query, cancellationToken) =>
            ((GetCompletedIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetStoppedIntrinsicTimeStrategyWorkflowsQuery)] = static (services, context, query, cancellationToken) =>
            ((GetStoppedIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowObservationQuery)] = static (services, context, query, cancellationToken) =>
            ((GetIntrinsicTimeStrategyWorkflowObservationQuery)query).ExecuteAsync(services, context, cancellationToken),
        [typeof(GetIntrinsicTimeStrategyWorkflowHistoryPageQuery)] = static (services, context, query, cancellationToken) =>
            IntrinsicTimeStrategyWorkflowQueryModel.ExecuteAsync(
                services, context, (GetIntrinsicTimeStrategyWorkflowHistoryPageQuery)query, cancellationToken)
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) =>
            exception is ProjectionSnapshotNotReadyException ? 25009 : query.ErrorCode);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

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
