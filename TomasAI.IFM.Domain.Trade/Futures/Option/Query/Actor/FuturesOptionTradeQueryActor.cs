using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Application.CommandPipeline;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
public sealed class FuturesOptionTradeQueryActor(IQueryActorContext<FuturesOptionTradeQueryActor> context) : BaseQueryActor<FuturesOptionTradeQueryActor>(context, Typed(context).Logger)
{
    public const string ActorName = FuturesOptionTradeActorNames.Query;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetTradeRealtimeSnapshotQuery.Verb] = message => message.AsQuery<GetTradeRealtimeSnapshotQuery, TradeRealtimeSnapshot>()!,
        [GetIronCondorOptionTradeQuery.Verb] = message => message.AsQuery<GetIronCondorOptionTradeQuery, EstablishedTradeDefinition>()!,
        [GetVerticalSpreadOptionTradeQuery.Verb] = message => message.AsQuery<GetVerticalSpreadOptionTradeQuery, EstablishedTradeDefinition>()!,
        [GetIronCondorOptionTradesQuery.Verb] = message => message.AsQuery<GetIronCondorOptionTradesQuery, EstablishedTradeDefinition[]>()!,
        [GetVerticalSpreadOptionTradesQuery.Verb] = message => message.AsQuery<GetVerticalSpreadOptionTradesQuery, EstablishedTradeDefinition[]>()!,
        [GetOptionTradeQuery.Verb] = message => message.AsQuery<GetOptionTradeQuery, OptionTradeReadModel>()!,
        [GetOptionTradesQuery.Verb] = message => message.AsQuery<GetOptionTradesQuery, OptionTradeReadModel[]>()!,
        [GetOptionTradeSpreadDataQuery.Verb] = message => message.AsQuery<GetOptionTradeSpreadDataQuery, OptionTradeSpreadsDataModel>()!,
        [GetOptionTradeSpreadBarDataQuery.Verb] = message => message.AsQuery<GetOptionTradeSpreadBarDataQuery, OptionTradeSpreadBarsDataModel[]>()!,
        [GetOptionLegContractIdsQuery.Verb] = message => message.AsQuery<GetOptionLegContractIdsQuery, string[]>()!,
        [GetIronCondorTradePriceQuery.Verb] = message => message.AsQuery<GetIronCondorTradePriceQuery, TradePriceReadModel>()!,
        [GetTradePositionsQuery.Verb] = message => message.AsQuery<GetTradePositionsQuery, TradePositionReadModel[]>()!,
        [GetTradePositionTradeTypesQuery.Verb] = message => message.AsQuery<GetTradePositionTradeTypesQuery, string[]>()!,
        [GetTradePlanActionQuery.Verb] = message => message.AsQuery<GetTradePlanActionQuery, TradePlanActionReadModel[]>()!,

        [GetTradeHistoryQuery.Verb] = message => message.AsQuery<GetTradeHistoryQuery, TradeHistoryReadModel[]>()!,
        [GetTradeLimitQuery.Verb] = message => message.AsQuery<GetTradeLimitQuery, TradeLimitReadModel>()!,
        [GetTradePositionQuery.Verb] = message => message.AsQuery<GetTradePositionQuery, TradePositionReadModel>()!,
        [GetTradeQuantityQuery.Verb] = message => message.AsQuery<GetTradeQuantityQuery, ScalarReadModel<int>>()!,
        [GetTradeTypeLimitQuery.Verb] = message => message.AsQuery<GetTradeTypeLimitQuery, TradeTypeLimitReadModel>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IFuturesOptionTradeQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesOptionTradeQueryContext, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetTradeRealtimeSnapshotQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradeRealtimeSnapshotQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIronCondorOptionTradeQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetIronCondorOptionTradeQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetVerticalSpreadOptionTradeQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetVerticalSpreadOptionTradeQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIronCondorOptionTradesQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetIronCondorOptionTradesQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetVerticalSpreadOptionTradesQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetVerticalSpreadOptionTradesQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOptionTradeQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetOptionTradeQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOptionTradesQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetOptionTradesQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOptionTradeSpreadDataQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetOptionTradeSpreadDataQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOptionTradeSpreadBarDataQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetOptionTradeSpreadBarDataQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetOptionLegContractIdsQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetOptionLegContractIdsQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetIronCondorTradePriceQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetIronCondorTradePriceQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradePositionsQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradePositionsQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradePositionTradeTypesQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradePositionTradeTypesQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradePlanActionQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradePlanActionQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradeHistoryQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradeHistoryQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradeLimitQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradeLimitQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradePositionQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradePositionQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradeQuantityQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradeQuantityQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        },
        [typeof(GetTradeTypeLimitQuery)] = static async (context, query, token) =>
        {
            var result = await ((GetTradeTypeLimitQuery)query).ExecuteAsync(context, token).ConfigureAwait(false);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
        }
    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys);
    protected override IQuery ParseMessage(IQueryActorContext<FuturesOptionTradeQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionTradeQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionTradeQueryActor> context, IQuery query, CancellationToken cancellationToken) => ResolveMappedQueryHandler(query, _receiveMap)(Typed(context), query, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<FuturesOptionTradeQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex) => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
    static IFuturesOptionTradeQueryContext Typed(IQueryActorContext<FuturesOptionTradeQueryActor> context) => context as IFuturesOptionTradeQueryContext ?? throw new ArgumentException("Typed Futures Option Trade query context required.", nameof(context));
}
