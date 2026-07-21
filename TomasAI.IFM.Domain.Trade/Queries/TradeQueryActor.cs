using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Queries.Handlers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

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
    IDbContextFactory dbFactory,
    ILogger<TradeQueryActor> logger)
    : BaseQueryActor<TradeQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "TradeQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
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
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IQuery>> _parseMap = new()
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
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(context, query.Subject.ThreadId, dbFactory, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding trade
    /// query logic against the database context factory.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, ActorThreadId, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetTradeHistoryQuery).Name] = (ctx, threadId, dbFactory, q) =>
        {
            var query = (q as GetTradeHistoryQuery)!;
            var msgInfo = ctx.GetMessageInfo(threadId, GetTradeHistoryQuery.Verb);
            return query.GetTradeHistoryAsync(dbFactory, msgInfo!.Value);
        },
        [typeof(GetTradeLimitQuery).Name] = (ctx, threadId, dbFactory, q) =>
        {
            var query = (q as GetTradeLimitQuery)!;
            var msgInfo = ctx.GetMessageInfo(threadId, GetTradeLimitQuery.Verb);
            return query.GetTradeLimitAsync(dbFactory, msgInfo!.Value);
        },
        [typeof(GetTradePositionQuery).Name] = (ctx, threadId, dbFactory, q) =>
        {
            var query = (q as GetTradePositionQuery)!;
            var msgInfo = ctx.GetMessageInfo(threadId, GetTradePositionQuery.Verb);
            return query.GetTradePositionAsync(dbFactory, msgInfo!.Value);
        },
        [typeof(GetTradeQuantityQuery).Name] = (ctx, threadId, dbFactory, q) =>
        {
            var query = (q as GetTradeQuantityQuery)!;
            var msgInfo = ctx.GetMessageInfo(threadId, GetTradeQuantityQuery.Verb);
            return query.GetTradeQuantityAsync(dbFactory, msgInfo!.Value);
        },
        [typeof(GetTradeTypeLimitQuery).Name] = (ctx, threadId, dbFactory, q) =>
        {
            var query = (q as GetTradeTypeLimitQuery)!;
            var msgInfo = ctx.GetMessageInfo(threadId, GetTradeTypeLimitQuery.Verb);
            return query.GetTradeTypeLimitAsync(dbFactory, msgInfo!.Value);
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
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}

