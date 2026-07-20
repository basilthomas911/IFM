using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

/// <summary>
/// Represents an actor responsible for managing futures ITI signal queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesItiSignalQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures intrinsic time indicator signal lookups such as retrieving signal data, MDI data,
/// MDI data by trend, last signal, and trend direction changed signals.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesItiSignalQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesItiSignalQueryActor> logger)
    : BaseQueryActor<FuturesItiSignalQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "FuturesItiSignalQuery";

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
            || !_parseMap.ContainsKey(msgSubject.Verb))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = _parseMap[msgSubject.Verb](message);
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
        [GetFuturesItiSignalDataQuery.Verb] = msg => msg.AsQuery<GetFuturesItiSignalDataQuery, FuturesItiSignalDataReadModel>()!,
        [GetFuturesItiSignalQuery.Verb] = msg => msg.AsQuery<GetFuturesItiSignalQuery, FuturesItiSignalV2ReadModel>()!,
        [GetFuturesItiTrendDirectionChangedSignalsQuery.Verb] = msg => msg.AsQuery<GetFuturesItiTrendDirectionChangedSignalsQuery, FuturesItiSignalV2ReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="state">The current state of the actor, which must be of type <see cref="FuturesItiSignalQueryState"/>.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        var resultTask = _receiveMap.ContainsKey(qryName)
            ? _receiveMap[qryName](context, dbFactory, query)
            : throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await resultTask;
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures ITI signal query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesItiSignalDataQuery).Name] = async (ctx, db, q) =>
        {
            var query = (q as GetFuturesItiSignalDataQuery)!;
            await query.GetFuturesItiSignalDataAsync(db, ctx);
        },
        [typeof(GetFuturesItiSignalQuery).Name] = async (ctx, db, q) =>
        {
            var query = (q as GetFuturesItiSignalQuery)!;
            await query.GetLastFuturesItiSignalAsync(db, ctx);
        },
        [typeof(GetFuturesItiTrendDirectionChangedSignalsQuery).Name] = async (ctx, db, q) =>
        {
            var query = (q as GetFuturesItiTrendDirectionChangedSignalsQuery)!;
            await query.GetFuturesItiTrendDirectionChangedSignalsAsync(db, ctx);
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
                _ when query is GetFuturesItiSignalDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesItiSignalDataReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetFuturesItiSignalQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesItiSignalV2ReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetFuturesItiTrendDirectionChangedSignalsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesItiSignalV2ReadModel[]>(query.ErrorCode, ex!.Message)),
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
