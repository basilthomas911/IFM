using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

using TomasAI.IFM.Domain.Trade.Option.Query.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing option trade queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="OptionTradeQueryActor"/> is a specialized query actor designed to handle operations
/// related to option trade lookups such as retrieving option trades, spread data, spread bar data,
/// option leg contract IDs, and iron condor trade prices.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="dbFactory">The database context factory used to access option trade data.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class OptionTradeQueryActor(
    IQueryActorContext<OptionTradeQueryActor> actorContext)
    : BaseQueryActor<OptionTradeQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IOptionTradeQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IOptionTradeQueryContext, nameof(Context))!;

    public const string ActorName = "OptionTradeQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<OptionTradeQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetOptionTradeQuery.Verb] = msg => msg.AsQuery<GetOptionTradeQuery, OptionTradeReadModel>()!,
        [GetOptionTradesQuery.Verb] = msg => msg.AsQuery<GetOptionTradesQuery, OptionTradeReadModel[]>()!,
        [GetOptionTradeSpreadDataQuery.Verb] = msg => msg.AsQuery<GetOptionTradeSpreadDataQuery, OptionTradeSpreadsDataModel>()!,
        [GetOptionTradeSpreadBarDataQuery.Verb] = msg => msg.AsQuery<GetOptionTradeSpreadBarDataQuery, OptionTradeSpreadBarsDataModel[]>()!,
        [GetOptionLegContractIdsQuery.Verb] = msg => msg.AsQuery<GetOptionLegContractIdsQuery, string[]>()!,
        [GetIronCondorTradePriceQuery.Verb] = msg => msg.AsQuery<GetIronCondorTradePriceQuery, TradePriceReadModel>()!,
        [GetTradePositionsQuery.Verb] = msg => msg.AsQuery<GetTradePositionsQuery, TradePositionReadModel[]>()!,
        [GetTradePositionTradeTypesQuery.Verb] = msg => msg.AsQuery<GetTradePositionTradeTypesQuery, string[]>()!,
        [GetTradePlanActionQuery.Verb] = msg => msg.AsQuery<GetTradePlanActionQuery, TradePlanActionReadModel[]>()!,
        [GetIronCondorMDILimitQuery.Verb] = msg => msg.AsQuery<GetIronCondorMDILimitQuery, IronCondorMDILimitDataModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<OptionTradeQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<OptionTradeQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        return receiveFunc.Invoke(dispatchContext, ActorContext.DbFactory, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding option trade query
    /// logic against the database context factory.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<OptionTradeQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<OptionTradeQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetOptionTradeQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeQuery)!;
            var result = await query.GetOptionTradeAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeQuery.Verb,
                new ServiceResult<OptionTradeReadModel?>(result));
        },
        [typeof(GetOptionTradesQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradesQuery)!;
            var result = await query.GetOptionTradesAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradesQuery.Verb,
                new ServiceResult<OptionTradeReadModel[]>(result));
        },
        [typeof(GetOptionTradeSpreadDataQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeSpreadDataQuery)!;
            var result = await query.GetOptionTradeSpreadDataAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadDataQuery.Verb,
                new ServiceResult<OptionTradeSpreadsDataModel>(result));
        },
        [typeof(GetOptionTradeSpreadBarDataQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeSpreadBarDataQuery)!;
            var result = await query.GetOptionTradeSpreadBarDataAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadBarDataQuery.Verb,
                new ServiceResult<OptionTradeSpreadBarsDataModel[]>(result));
        },
        [typeof(GetOptionLegContractIdsQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionLegContractIdsQuery)!;
            var result = await query.GetOptionLegContractIdsAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionLegContractIdsQuery.Verb,
                new ServiceResult<string[]>(result));
        },
        [typeof(GetIronCondorTradePriceQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetIronCondorTradePriceQuery)!;
            var result = await query.GetIronCondorTradePriceAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetIronCondorTradePriceQuery.Verb,
                new ServiceResult<TradePriceReadModel?>(result));
        },
        [typeof(GetTradePositionsQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetTradePositionsQuery)q;
            var result = await dbFactory.TradeDb.GetTradePositionsAsync(
                query.OrderId, query.TradeId, cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePositionsQuery.Verb,
                new ServiceResult<TradePositionReadModel[]>([.. result]));
        },
        [typeof(GetTradePositionTradeTypesQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetTradePositionTradeTypesQuery)q;
            var result = await dbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                query.OrderId,
                query.TradeId,
                query.ValueDate,
                query.TradeStatus,
                query.DaysToExpiry,
                cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePositionTradeTypesQuery.Verb,
                new ServiceResult<string[]>([.. result]));
        },
        [typeof(GetTradePlanActionQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePlanActionQuery.Verb,
                new ServiceResult<TradePlanActionReadModel[]>([]));
        },
        [typeof(GetIronCondorMDILimitQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetIronCondorMDILimitQuery)q;
            cancellationToken.ThrowIfCancellationRequested();
            var result = ctx.BlackboardService.Trade.IronCondorMDILimit.Get(
                new OptionTradeEntityId(query.OrderId, query.TradeId), query.ValueDate);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetIronCondorMDILimitQuery.Verb,
                new ServiceResult<IronCondorMDILimitDataModel?>(result));
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that caused the exception.</param>
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<OptionTradeQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
