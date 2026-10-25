using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;
public sealed class PositionExitWorkflowQueryActor(IQueryActorContext<PositionExitWorkflowQueryActor> context) : BaseQueryActor<PositionExitWorkflowQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetPositionExitWorkflowQuery.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetPositionExitWorkflowQuery.Verb] = message => message.AsQuery<GetPositionExitWorkflowQuery, ExitPositionWorkflowProjection?>()!,
        [GetPositionExitWorkflowTimelineQuery.Verb] = message => message.AsQuery<GetPositionExitWorkflowTimelineQuery, PositionExitWorkflowHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IPositionExitWorkflowQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IPositionExitWorkflowQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetPositionExitWorkflowQuery)] = static (owner, query, token) => ((GetPositionExitWorkflowQuery)query).ExecuteAsync(owner, token),
        [typeof(GetPositionExitWorkflowTimelineQuery)] = static (owner, query, token) => ((GetPositionExitWorkflowTimelineQuery)query).ExecuteAsync(owner, token)
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<PositionExitWorkflowQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<PositionExitWorkflowQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<PositionExitWorkflowQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<PositionExitWorkflowQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IPositionExitWorkflowQueryContext Typed(IQueryActorContext<PositionExitWorkflowQueryActor> context) => context as IPositionExitWorkflowQueryContext ?? throw new ArgumentException("Typed position exit-workflow query context required.", nameof(context));
}
