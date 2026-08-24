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
    : BaseQueryActor<OptionTradeQueryActor>(actorContext.Logger, actorContext.ActorId)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IOptionTradeQueryContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IOptionTradeQueryContext, nameof(actorContext))!;

    public const string ActorName = "OptionTradeQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = messageParser.Invoke(message);
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            verb: msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
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
    protected override ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = actorContext.RouteTo(context);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        return receiveFunc.Invoke(dispatchContext, actorContext.DbFactory, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding option trade query
    /// logic against the database context factory.
    /// </summary>
    readonly Dictionary<string, Func<IQueryActorContext<OptionTradeQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetOptionTradeQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeQuery)!;
            var result = await query.GetOptionTradeAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeQuery.Verb,
                new ServiceResult<OptionTradeReadModel?>(result));
        },
        [typeof(GetOptionTradesQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradesQuery)!;
            var result = await query.GetOptionTradesAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradesQuery.Verb,
                new ServiceResult<OptionTradeReadModel[]>(result));
        },
        [typeof(GetOptionTradeSpreadDataQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeSpreadDataQuery)!;
            var result = await query.GetOptionTradeSpreadDataAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadDataQuery.Verb,
                new ServiceResult<OptionTradeSpreadsDataModel>(result));
        },
        [typeof(GetOptionTradeSpreadBarDataQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionTradeSpreadBarDataQuery)!;
            var result = await query.GetOptionTradeSpreadBarDataAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadBarDataQuery.Verb,
                new ServiceResult<OptionTradeSpreadBarsDataModel[]>(result));
        },
        [typeof(GetOptionLegContractIdsQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetOptionLegContractIdsQuery)!;
            var result = await query.GetOptionLegContractIdsAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetOptionLegContractIdsQuery.Verb,
                new ServiceResult<string[]>(result));
        },
        [typeof(GetIronCondorTradePriceQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetIronCondorTradePriceQuery)!;
            var result = await query.GetIronCondorTradePriceAsync(actorContext.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetIronCondorTradePriceQuery.Verb,
                new ServiceResult<TradePriceReadModel?>(result));
        },
        [typeof(GetTradePositionsQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetTradePositionsQuery)q;
            var result = await actorContext.DbFactory.TradeDb.GetTradePositionsAsync(
                query.OrderId, query.TradeId, cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePositionsQuery.Verb,
                new ServiceResult<TradePositionReadModel[]>([.. result]));
        },
        [typeof(GetTradePositionTradeTypesQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetTradePositionTradeTypesQuery)q;
            var result = await actorContext.DbFactory.TradeDb.GetTradePositionTradeTypesAsync(
                query.OrderId,
                query.TradeId,
                query.ValueDate,
                query.TradeStatus,
                query.DaysToExpiry,
                cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePositionTradeTypesQuery.Verb,
                new ServiceResult<string[]>([.. result]));
        },
        [typeof(GetTradePlanActionQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePlanActionQuery.Verb,
                new ServiceResult<TradePlanActionReadModel[]>([]));
        },
        [typeof(GetIronCondorMDILimitQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetIronCondorMDILimitQuery)q;
            cancellationToken.ThrowIfCancellationRequested();
            var result = actorContext.BlackboardService.Trade.IronCondorMDILimit.Get(
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
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(ex?.Message!);

        try
        {
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetOptionTradeQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradeSpreadDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeSpreadsDataModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradeSpreadBarDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeSpreadBarsDataModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionLegContractIdsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<string[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetIronCondorTradePriceQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradePriceReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradePositionsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradePositionReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradePositionTradeTypesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<string[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradePlanActionQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradePlanActionReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetIronCondorMDILimitQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<IronCondorMDILimitDataModel?>(query.ErrorCode, ex!.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex!.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            actorContext.Logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}
