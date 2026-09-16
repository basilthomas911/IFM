using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;

/// <summary>Answers broker-order reads from committed projection state.</summary>
public sealed class BrokerOrderQueryActor(IQueryActorContext<BrokerOrderQueryActor> context)
    : BaseQueryActor<BrokerOrderQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerOrderActorNames.Query;

    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetBrokerOrderQuery.Verb] = message =>
                message.AsQuery<GetBrokerOrderQuery, BrokerOrderDefinition>()!,
            [GetBrokerOrdersForTradeOrderQuery.Verb] = message =>
                message.AsQuery<GetBrokerOrdersForTradeOrderQuery, BrokerOrderDefinition[]>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<Type,
        Func<IBrokerOrderQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IBrokerOrderQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetBrokerOrderQuery)] = static (owner, query, token) =>
                ((GetBrokerOrderQuery)query).ExecuteAsync(owner, token),
            [typeof(GetBrokerOrdersForTradeOrderQuery)] = static (owner, query, token) =>
                ((GetBrokerOrdersForTradeOrderQuery)query).ExecuteAsync(owner, token)
        }.ToFrozenDictionary();

    private static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<BrokerOrderQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<BrokerOrderQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<BrokerOrderQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<BrokerOrderQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => ExceptionMappedQueryAsync(
            context, threadId, query, verb, exception, _exceptionMap);

    private static IBrokerOrderQueryContext Typed(IQueryActorContext<BrokerOrderQueryActor> context) =>
        context as IBrokerOrderQueryContext ??
        throw new ArgumentException("Typed BrokerOrder query context required.");
}
