using System.Collections.Frozen;
using TomasAI.IFM.Domain.BrokerAccount.Query;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Actor;

/// <summary>Answers current BrokerAccount snapshot and gate queries.</summary>
public sealed class BrokerAccountQueryActor(IQueryActorContext<BrokerAccountQueryActor> context)
    : BaseQueryActor<BrokerAccountQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerAccountActorNames.Query;

    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetBrokerAccountQuery.Verb] = message =>
                message.AsQuery<GetBrokerAccountQuery, BrokerAccountDefinition>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<Type,
        Func<IBrokerAccountQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IBrokerAccountQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetBrokerAccountQuery)] = static (owner, query, token) =>
                ((GetBrokerAccountQuery)query).ExecuteAsync(owner, token)
        }.ToFrozenDictionary();

    private static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override IQuery ParseMessage(IQueryActorContext<BrokerAccountQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<BrokerAccountQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(IQueryActorContext<BrokerAccountQueryActor> context,
        IQuery query, CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(IQueryActorContext<BrokerAccountQueryActor> context,
        ActorThreadId threadId, IQuery query, string verb, Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

    private static IBrokerAccountQueryContext Typed(IQueryActorContext<BrokerAccountQueryActor> context) =>
        context as IBrokerAccountQueryContext ??
        throw new ArgumentException("Typed BrokerAccount query context required.");
}
