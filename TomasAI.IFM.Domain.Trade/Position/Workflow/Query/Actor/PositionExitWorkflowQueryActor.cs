using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Query.Actor;

public sealed class PositionExitWorkflowQueryActor(
    IQueryActorContext<PositionExitWorkflowQueryActor> context)
    : BaseQueryActor<PositionExitWorkflowQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetPositionExitWorkflowQuery.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetPositionExitWorkflowQuery.Verb] = message =>
                message.AsQuery<GetPositionExitWorkflowQuery,
                    ExitPositionWorkflowProjection?>()!,
            [GetPositionExitWorkflowTimelineQuery.Verb] = message =>
                message.AsQuery<GetPositionExitWorkflowTimelineQuery,
                    PositionExitWorkflowHistoryPage>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IPositionExitWorkflowQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type,
            Func<IPositionExitWorkflowQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetPositionExitWorkflowQuery)] = static (owner, query, token) =>
                ((GetPositionExitWorkflowQuery)query).ExecuteAsync(owner, token),
            [typeof(GetPositionExitWorkflowTimelineQuery)] = static (owner, query, token) =>
                ((GetPositionExitWorkflowTimelineQuery)query).ExecuteAsync(owner, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<PositionExitWorkflowQueryActor> actorContext,
        IActorMessage message) => ParseMappedQuery(actorContext, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<PositionExitWorkflowQueryActor> actorContext,
        IQuery query) => ReceiveAsync(actorContext, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<PositionExitWorkflowQueryActor> actorContext,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, ReceiveMap)(Typed(actorContext), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<PositionExitWorkflowQueryActor> actorContext,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(actorContext, threadId, query, verb, exception, ExceptionMap);

    static IPositionExitWorkflowQueryContext Typed(
        IQueryActorContext<PositionExitWorkflowQueryActor> context) =>
        context as IPositionExitWorkflowQueryContext ??
        throw new ArgumentException("Typed position exit-workflow query context required.");
}

public interface IPositionExitWorkflowQueryContext :
    IQueryActorContext<PositionExitWorkflowQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<PositionExitWorkflowQueryActor> Logger { get; }
}

public sealed class PositionExitWorkflowQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<PositionExitWorkflowQueryActor> logger)
    : QueryActorContext(supervisor,
        new(ActorType.Query, PositionExitWorkflowQueryActor.ActorName)),
      IPositionExitWorkflowQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<PositionExitWorkflowQueryActor> Logger { get; } = logger;
}
