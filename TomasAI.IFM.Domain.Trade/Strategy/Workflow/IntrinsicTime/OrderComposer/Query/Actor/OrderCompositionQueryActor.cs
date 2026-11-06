using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query;
using TomasAI.IFM.Application.Serialization;
using System.Collections.Frozen;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Query.Actor;
public sealed class OrderCompositionQueryActor(IQueryActorContext<OrderCompositionQueryActor> context) : BaseQueryActor<OrderCompositionQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = GetOrderCompositionInvocationQuery.Actor;
    readonly IOrderCompositionQueryContext services = Typed(context);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetOrderCompositionInvocationQuery.Verb] = m => m.AsQuery<GetOrderCompositionInvocationQuery, OrderCompositionProjection>()!,
        [GetOrderCompositionResultQuery.Verb] = m => m.AsQuery<GetOrderCompositionResultQuery, OrderCompositionResult>()!,
        [GetOrderCompositionHistoryPageQuery.Verb] = m => m.AsQuery<GetOrderCompositionHistoryPageQuery, OrderCompositionHistoryPage>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<OrderCompositionQueryActor, IQueryActorContext<OrderCompositionQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<OrderCompositionQueryActor, IQueryActorContext<OrderCompositionQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetOrderCompositionInvocationQuery)] = static async (actor, context, query, token) =>
        {
            var result = await ((GetOrderCompositionInvocationQuery)query).ExecuteAsync(actor.services, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOrderCompositionResultQuery)] = static async (actor, context, query, token) =>
        {
            var result = await ((GetOrderCompositionResultQuery)query).ExecuteAsync(actor.services, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOrderCompositionHistoryPageQuery)] = static async (actor, context, query, token) =>
        {
            var result = await ((GetOrderCompositionHistoryPageQuery)query).ExecuteAsync(actor.services, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<OrderCompositionQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderCompositionQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderCompositionQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(this, context, query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<OrderCompositionQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IOrderCompositionQueryContext Typed(IQueryActorContext<OrderCompositionQueryActor> c) => c as IOrderCompositionQueryContext ?? throw new ArgumentException("Typed composition query context required.", nameof(c));
}
