using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Queries.Handlers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

using TomasAI.IFM.Domain.Trade.Queries;

namespace TomasAI.IFM.Domain.Trade.Queries;

/// <summary>
/// Represents an actor responsible for managing trade queries within an event-sourced system.
/// </summary>
/// <remarks>
/// The <see cref="TradeQueryActor"/> is a specialised query actor designed to handle trade lookups
/// such as trade history, trade limit, trade position, trade quantity, and trade type limit retrieval.
/// It processes queries, validates them, and manages the actor's state.
/// </remarks>
/// <param name="dbFactory">The database context factory used to access trade data.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class TradeQueryActor(
    IQueryActorContext<TradeQueryActor> actorContext)
    : BaseQueryActor<TradeQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected ITradeQueryContext ActorContext =>
        IsArgumentNull.Set(Context as ITradeQueryContext, nameof(Context))!;

    public const string ActorName = "TradeQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<TradeQueryActor> context, IActorMessage message)
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
        [GetTradeHistoryQuery.Verb] = msg => msg.AsQuery<GetTradeHistoryQuery, TradeHistoryReadModel[]>()!,
        [GetTradeLimitQuery.Verb] = msg => msg.AsQuery<GetTradeLimitQuery, TradeLimitReadModel>()!,
        [GetTradePositionQuery.Verb] = msg => msg.AsQuery<GetTradePositionQuery, TradePositionReadModel>()!,
        [GetTradeQuantityQuery.Verb] = msg => msg.AsQuery<GetTradeQuantityQuery, ScalarReadModel<int>>()!,
        [GetTradeTypeLimitQuery.Verb] = msg => msg.AsQuery<GetTradeTypeLimitQuery, TradeTypeLimitReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<TradeQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override ValueTask ReceiveAsync(
        IQueryActorContext<TradeQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        return receiveFunc.Invoke(dispatchContext, ActorContext.DbFactory, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding trade
    /// query logic against the database context factory.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext<TradeQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetTradeHistoryQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetTradeHistoryQuery)!;
            var result = await query.GetTradeHistoryAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeHistoryQuery.Verb,
                new ServiceResult<TradeHistoryReadModel[]>(result));
        },
        [typeof(GetTradeLimitQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetTradeLimitQuery)!;
            var result = await query.GetTradeLimitAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeLimitQuery.Verb,
                new ServiceResult<TradeLimitReadModel>(result));
        },
        [typeof(GetTradePositionQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetTradePositionQuery)!;
            var result = await query.GetTradePositionAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradePositionQuery.Verb,
                new ServiceResult<TradePositionReadModel>(result));
        },
        [typeof(GetTradeQuantityQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetTradeQuantityQuery)!;
            var result = await query.GetTradeQuantityAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeQuantityQuery.Verb,
                new ServiceResult<ScalarReadModel<int>>(result));
        },
        [typeof(GetTradeTypeLimitQuery).Name] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetTradeTypeLimitQuery)!;
            var result = await query.GetTradeTypeLimitAsync(dbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeTypeLimitQuery.Verb,
                new ServiceResult<TradeTypeLimitReadModel>(result));
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
    protected override async ValueTask OnExceptionAsync(IQueryActorContext<TradeQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
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
                _ when query is GetTradeHistoryQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradeHistoryReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradeLimitQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradeLimitReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradePositionQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradePositionReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradeQuantityQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarReadModel<int>>(query.ErrorCode, ex!.Message)),
                _ when query is GetTradeTypeLimitQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradeTypeLimitReadModel>(query.ErrorCode, ex!.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex!.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            Context.Logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}

