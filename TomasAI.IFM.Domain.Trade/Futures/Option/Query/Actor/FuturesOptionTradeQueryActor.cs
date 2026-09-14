using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
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
                message.AsQuery<GetVerticalSpreadOptionTradesQuery, EstablishedTradeDefinition[]>()!,
            [GetOptionTradeQuery.Verb] = message =>
                message.AsQuery<GetOptionTradeQuery, OptionTradeReadModel>()!,
            [GetOptionTradesQuery.Verb] = message =>
                message.AsQuery<GetOptionTradesQuery, OptionTradeReadModel[]>()!,
            [GetOptionTradeSpreadDataQuery.Verb] = message =>
                message.AsQuery<GetOptionTradeSpreadDataQuery, OptionTradeSpreadsDataModel>()!,
            [GetOptionTradeSpreadBarDataQuery.Verb] = message =>
                message.AsQuery<GetOptionTradeSpreadBarDataQuery, OptionTradeSpreadBarsDataModel[]>()!,
            [GetOptionLegContractIdsQuery.Verb] = message =>
                message.AsQuery<GetOptionLegContractIdsQuery, string[]>()!,
            [GetIronCondorTradePriceQuery.Verb] = message =>
                message.AsQuery<GetIronCondorTradePriceQuery, TradePriceReadModel>()!,
            [GetTradePositionsQuery.Verb] = message =>
                message.AsQuery<GetTradePositionsQuery, TradePositionReadModel[]>()!,
            [GetTradePositionTradeTypesQuery.Verb] = message =>
                message.AsQuery<GetTradePositionTradeTypesQuery, string[]>()!,
            [GetTradePlanActionQuery.Verb] = message =>
                message.AsQuery<GetTradePlanActionQuery, TradePlanActionReadModel[]>()!,
            [GetIronCondorMDILimitQuery.Verb] = message =>
                message.AsQuery<GetIronCondorMDILimitQuery, IronCondorMDILimitDataModel>()!,
            [GetTradeHistoryQuery.Verb] = message =>
                message.AsQuery<GetTradeHistoryQuery, TradeHistoryReadModel[]>()!,
            [GetTradeLimitQuery.Verb] = message =>
                message.AsQuery<GetTradeLimitQuery, TradeLimitReadModel>()!,
            [GetTradePositionQuery.Verb] = message =>
                message.AsQuery<GetTradePositionQuery, TradePositionReadModel>()!,
            [GetTradeQuantityQuery.Verb] = message =>
                message.AsQuery<GetTradeQuantityQuery, ScalarReadModel<int>>()!,
            [GetTradeTypeLimitQuery.Verb] = message =>
                message.AsQuery<GetTradeTypeLimitQuery, TradeTypeLimitReadModel>()!
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
                ((GetVerticalSpreadOptionTradesQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetOptionTradeQuery)] = static (queryContext, query, token) =>
                ((GetOptionTradeQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetOptionTradesQuery)] = static (queryContext, query, token) =>
                ((GetOptionTradesQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetOptionTradeSpreadDataQuery)] = static (queryContext, query, token) =>
                ((GetOptionTradeSpreadDataQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetOptionTradeSpreadBarDataQuery)] = static (queryContext, query, token) =>
                ((GetOptionTradeSpreadBarDataQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetOptionLegContractIdsQuery)] = static (queryContext, query, token) =>
                ((GetOptionLegContractIdsQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetIronCondorTradePriceQuery)] = static (queryContext, query, token) =>
                ((GetIronCondorTradePriceQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradePositionsQuery)] = static (queryContext, query, token) =>
                ((GetTradePositionsQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradePositionTradeTypesQuery)] = static (queryContext, query, token) =>
                ((GetTradePositionTradeTypesQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradePlanActionQuery)] = static (queryContext, query, token) =>
                ((GetTradePlanActionQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetIronCondorMDILimitQuery)] = static (queryContext, query, token) =>
                ((GetIronCondorMDILimitQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradeHistoryQuery)] = static (queryContext, query, token) =>
                ((GetTradeHistoryQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradeLimitQuery)] = static (queryContext, query, token) =>
                ((GetTradeLimitQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradePositionQuery)] = static (queryContext, query, token) =>
                ((GetTradePositionQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradeQuantityQuery)] = static (queryContext, query, token) =>
                ((GetTradeQuantityQuery)query).ExecuteAsync(queryContext, token),
            [typeof(GetTradeTypeLimitQuery)] = static (queryContext, query, token) =>
                ((GetTradeTypeLimitQuery)query).ExecuteAsync(queryContext, token)
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
    IBlackboardService BlackboardService { get; }
    ILogger<FuturesOptionTradeQueryActor> Logger { get; }
}

public sealed class FuturesOptionTradeQueryContext(
    IActorSupervisor supervisor,
    IDbContextFactory dbFactory,
    IBlackboardService blackboardService,
    ILogger<FuturesOptionTradeQueryActor> logger)
    : QueryActorContext(
        supervisor,
        new ActorMailboxId(ActorType.Query, FuturesOptionTradeQueryActor.ActorName)),
        IQueryActorContext<FuturesOptionTradeQueryActor>,
        IFuturesOptionTradeQueryContext
{
    public IDbContextFactory DbFactory { get; } = dbFactory;
    public IBlackboardService BlackboardService { get; } = blackboardService;
    public ILogger<FuturesOptionTradeQueryActor> Logger { get; } = logger;
}
