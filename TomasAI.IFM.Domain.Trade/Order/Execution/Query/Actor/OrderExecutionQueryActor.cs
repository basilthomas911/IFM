using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Order.Execution.Query;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Query.Actor;
public sealed class OrderExecutionQueryActor(IQueryActorContext<OrderExecutionQueryActor> context) : BaseQueryActor<OrderExecutionQueryActor>(context, T(context).Logger)
{
    public const string ActorName = OrderExecutionActorNames.Query;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetOrderExecutionQuery.Verb] = static message => message.AsQuery<GetOrderExecutionQuery, OrderExecutionDefinition>()!,
            [GetFundOrderFillsQuery.Verb] = static message => message.AsQuery<GetFundOrderFillsQuery, FundOrderFillsRow[]>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IOrderExecutionQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IOrderExecutionQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetFundOrderFillsQuery)] = static async (context, query, cancellationToken) =>
            {
                var result = await ((GetFundOrderFillsQuery)query).ExecuteAsync(context.DbFactory.TradeDb, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<FundOrderFillsRow[]>(result)).ConfigureAwait(false);
            },
            [typeof(GetOrderExecutionQuery)] = static async (context, query, cancellationToken) =>
            {
                var result = await ((GetOrderExecutionQuery)query).ExecuteAsync(context.DbFactory.TradeDb, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
                    new ServiceResult<OrderExecutionDefinition?>(result)).ConfigureAwait(false);
            }
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<OrderExecutionQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderExecutionQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<OrderExecutionQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(T(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<OrderExecutionQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IOrderExecutionQueryContext T(IQueryActorContext<OrderExecutionQueryActor> c) => c as IOrderExecutionQueryContext ?? throw new ArgumentException("Typed execution query context required.", nameof(c));
}
