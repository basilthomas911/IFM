using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Query.Actor;

public sealed class FuturesTradeQueryActor(
    IQueryActorContext<FuturesTradeQueryActor> context)
    : BaseQueryActor<FuturesTradeQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesTradeActorNames.Query;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetFuturesTradeQuery.Verb] = message =>
                message.AsQuery<GetFuturesTradeQuery, EstablishedTradeDefinition>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesTradeQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type,
            Func<IFuturesTradeQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetFuturesTradeQuery)] = static (queryContext, query, token) =>
                ((GetFuturesTradeQuery)query).ExecuteAsync(queryContext, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesTradeQueryActor> context,
        IActorMessage message) =>
        ParseMappedQuery(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesTradeQueryActor> context,
        IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesTradeQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, ReceiveMap)(
            Typed(context),
            query,
            cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesTradeQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(
            context,
            threadId,
            query,
            verb,
            exception,
            ExceptionMap);

    static IFuturesTradeQueryContext Typed(
        IQueryActorContext<FuturesTradeQueryActor> context) =>
        context as IFuturesTradeQueryContext
        ?? throw new ArgumentException("Typed Futures Trade query context required.");
}

public interface IFuturesTradeQueryContext : IQueryActorContext<FuturesTradeQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesTradeQueryActor> Logger { get; }
}

public sealed class FuturesTradeQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<FuturesTradeQueryActor> logger)
    : QueryActorContext(
        supervisor,
        new ActorMailboxId(ActorType.Query, FuturesTradeQueryActor.ActorName)),
        IQueryActorContext<FuturesTradeQueryActor>,
        IFuturesTradeQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesTradeQueryActor> Logger { get; } = logger;
}
