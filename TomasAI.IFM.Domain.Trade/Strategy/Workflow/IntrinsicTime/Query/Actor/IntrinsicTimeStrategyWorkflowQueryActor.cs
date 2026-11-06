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

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Actor;
/// <summary>Serves side-effect-free Intrinsic Time Strategy workflow projections from cache and ScyllaDB.</summary>
public sealed class IntrinsicTimeStrategyWorkflowQueryActor(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> actorContext) : BaseQueryActor<IntrinsicTimeStrategyWorkflowQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
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
        [GetIntrinsicTimeStrategyWorkflowHistoryPageQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyWorkflowHistoryPageQuery, IntrinsicTimeStrategyWorkflowHistoryPageReadModel>()!,
        [GetIntrinsicTimeStrategyUpdatesGridQuery.Verb] = message => message.AsQuery<GetIntrinsicTimeStrategyUpdatesGridQuery, IntrinsicTimeStrategyUpdatesGridPage>()!
    };
    /// <summary>Gets the Query actor name.</summary>
    public const string ActorName = GetIntrinsicTimeStrategyWorkflowByIdQuery.Actor;
    IIntrinsicTimeStrategyWorkflowQueryContext ActorContext { get; } = RequireContext(actorContext);

    /// <inheritdoc/>
    protected override IQuery ParseMessage(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    /// <inheritdoc/>
    protected override ValueTask ReceiveAsync(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    /// <inheritdoc/>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(ActorContext, context, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly IReadOnlyDictionary<Type, Func<IIntrinsicTimeStrategyWorkflowQueryContext, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IIntrinsicTimeStrategyWorkflowQueryContext, IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetIntrinsicTimeStrategyWorkflowByIdQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowByIdQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowsByIdsQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowsByIdsQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetActiveIntrinsicTimeStrategyWorkflowQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetActiveIntrinsicTimeStrategyWorkflowQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowStartAttemptsQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowStageStateQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowStageStateQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowTimelineQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowTimelineQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetRecentIntrinsicTimeStrategyWorkflowsQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetRecentIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetCompletedIntrinsicTimeStrategyWorkflowsQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetCompletedIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetStoppedIntrinsicTimeStrategyWorkflowsQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetStoppedIntrinsicTimeStrategyWorkflowsQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowObservationQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowObservationQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyWorkflowHistoryPageQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyWorkflowHistoryPageQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIntrinsicTimeStrategyUpdatesGridQuery)] = static async (services, context, query, cancellationToken) =>
        {
            var result = await ((GetIntrinsicTimeStrategyUpdatesGridQuery)query).ExecuteAsync(services, cancellationToken).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        }
    };
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) => exception is ProjectionSnapshotNotReadyException ? 25009 : query.ErrorCode);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IIntrinsicTimeStrategyWorkflowQueryContext RequireContext(IQueryActorContext<IntrinsicTimeStrategyWorkflowQueryActor> context) => context as IIntrinsicTimeStrategyWorkflowQueryContext ?? throw new ArgumentException($"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowQueryContext)}.", nameof(context));
}
