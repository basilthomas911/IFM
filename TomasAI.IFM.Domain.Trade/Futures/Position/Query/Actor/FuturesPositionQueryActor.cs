using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;

public sealed class FuturesPositionQueryActor(IQueryActorContext<FuturesPositionQueryActor> context)
    : BaseQueryActor<FuturesPositionQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesPositionActorNames.Query;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetFuturesTradePositionQuery.Verb] = message =>
                message.AsQuery<GetFuturesTradePositionQuery, StrategyPositionSnapshot>()!,
            [GetFuturesTradePositionHistoryQuery.Verb] = message =>
                message.AsQuery<GetFuturesTradePositionHistoryQuery, StrategyPositionSnapshot[]>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesPositionQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type, Func<IFuturesPositionQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetFuturesTradePositionQuery)] = static (context, query, token) =>
                ((GetFuturesTradePositionQuery)query).ExecuteAsync(context, token),
            [typeof(GetFuturesTradePositionHistoryQuery)] = static (context, query, token) =>
                ((GetFuturesTradePositionHistoryQuery)query).ExecuteAsync(context, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesPositionQueryActor> context,
        IActorMessage message) => ParseMappedQuery(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesPositionQueryActor> context,
        IQuery query) => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesPositionQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, ReceiveMap)(Typed(context), query, cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesPositionQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) => ExceptionMappedQueryAsync(
            context, threadId, query, verb, exception, ExceptionMap);

    static IFuturesPositionQueryContext Typed(IQueryActorContext<FuturesPositionQueryActor> context) =>
        context as IFuturesPositionQueryContext ??
        throw new ArgumentException("Typed Futures position query context required.");
}

public interface IFuturesPositionQueryContext : IQueryActorContext<FuturesPositionQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesPositionQueryActor> Logger { get; }
}

public sealed class FuturesPositionQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<FuturesPositionQueryActor> logger)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, FuturesPositionQueryActor.ActorName)),
        IQueryActorContext<FuturesPositionQueryActor>, IFuturesPositionQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesPositionQueryActor> Logger { get; } = logger;
}
