using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

/// <summary>
/// Represents an actor responsible for managing futures bar data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesBarDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures bar data lookups such as retrieving futures bar data by contract/symbol/date range
/// and retrieving the most recent futures bar data.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesBarDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesBarDataQueryActor> logger)
    : BaseQueryActor<FuturesBarDataQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "FuturesBarDataQuery";

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
        [GetFuturesBarDataQuery.Verb] = msg => msg.AsQuery<GetFuturesBarDataQuery, FuturesBarDataReadModel[]>()!,
        [GetLastFuturesBarDataQuery.Verb] = msg => msg.AsQuery<GetLastFuturesBarDataQuery, FuturesBarDataReadModel>()!
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
        await receiveFunc.Invoke(context, dbFactory, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures bar data query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesBarDataQuery).Name] = (ctx,  dbFactory, q) =>
        {
            var query = (q as GetFuturesBarDataQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetFuturesBarDataQuery.Verb);
            return query.GetFuturesBarDataAsync(ctx, dbFactory);
        },
        [typeof(GetLastFuturesBarDataQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetLastFuturesBarDataQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetLastFuturesBarDataQuery.Verb);
            return query.GetLastFuturesBarDataAsync(ctx, dbFactory);
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
                _ when query is GetFuturesBarDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesBarDataReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetLastFuturesBarDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesBarDataReadModel[]>(query.ErrorCode, ex!.Message)),
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
