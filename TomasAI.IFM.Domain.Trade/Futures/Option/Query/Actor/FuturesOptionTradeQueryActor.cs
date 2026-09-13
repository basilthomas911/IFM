using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;

public sealed class FuturesOptionTradeQueryActor(
    IQueryActorContext<FuturesOptionTradeQueryActor> context)
    : BaseQueryActor<FuturesOptionTradeQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesOptionTradeActorNames.Query;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            [GetIronCondorOptionTradeQuery.Verb] = message =>
                message.AsQuery<GetIronCondorOptionTradeQuery, EstablishedTradeDefinition>()!,
            [GetVerticalSpreadOptionTradeQuery.Verb] = message =>
                message.AsQuery<GetVerticalSpreadOptionTradeQuery, EstablishedTradeDefinition>()!,
            [GetIronCondorOptionTradesQuery.Verb] = message =>
                message.AsQuery<GetIronCondorOptionTradesQuery, EstablishedTradeDefinition[]>()!,
            [GetVerticalSpreadOptionTradesQuery.Verb] = message =>
                message.AsQuery<GetVerticalSpreadOptionTradesQuery, EstablishedTradeDefinition[]>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type,
        Func<IFuturesOptionTradeQueryContext, IQuery, CancellationToken, ValueTask>> ReceiveMap =
        new Dictionary<Type,
            Func<IFuturesOptionTradeQueryContext, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetIronCondorOptionTradeQuery)] = static (queryContext, query, token) =>
                ((GetIronCondorOptionTradeQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetVerticalSpreadOptionTradeQuery)] = static (queryContext, query, token) =>
                ((GetVerticalSpreadOptionTradeQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetIronCondorOptionTradesQuery)] = static (queryContext, query, token) =>
                ((GetIronCondorOptionTradesQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetVerticalSpreadOptionTradesQuery)] = static (queryContext, query, token) =>
                ((GetVerticalSpreadOptionTradesQuery)query).ExecuteAsync(queryContext, token)
        }.ToFrozenDictionary();

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> ExceptionMap =
        CreateQueryExceptionMap(ReceiveMap.Keys);

    protected override IQuery ParseMessage(
        IQueryActorContext<FuturesOptionTradeQueryActor> context,
        IActorMessage message) =>
        ParseMappedQuery(context, message, ParseMap);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesOptionTradeQueryActor> context,
        IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<FuturesOptionTradeQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken) =>
        ResolveMappedQueryHandler(query, ReceiveMap)(
            Typed(context),
            query,
            cancellationToken);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesOptionTradeQueryActor> context,
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

    static IFuturesOptionTradeQueryContext Typed(
        IQueryActorContext<FuturesOptionTradeQueryActor> context) =>
        context as IFuturesOptionTradeQueryContext
        ?? throw new ArgumentException(
            "Typed Futures Option Trade query context required.");
}

public interface IFuturesOptionTradeQueryContext :
    IQueryActorContext<FuturesOptionTradeQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesOptionTradeQueryActor> Logger { get; }
}

public sealed class FuturesOptionTradeQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    ILogger<FuturesOptionTradeQueryActor> logger)
    : QueryActorContext(
        supervisor,
        new ActorMailboxId(ActorType.Query, FuturesOptionTradeQueryActor.ActorName)),
        IQueryActorContext<FuturesOptionTradeQueryActor>,
        IFuturesOptionTradeQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public ILogger<FuturesOptionTradeQueryActor> Logger { get; } = logger;
}
