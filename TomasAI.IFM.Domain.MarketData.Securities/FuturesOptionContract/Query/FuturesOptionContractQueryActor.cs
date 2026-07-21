using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

/// <summary>
/// Represents an actor responsible for managing futures option contract queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesOptionContractQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures option contracts. It processes queries such as retrieving specific contracts by ID,
/// contracts by symbol, and checking for existing contract IDs. This actor uses dependency injection to resolve required services.</remarks>
/// <param name="logger">The logger instance for tracking actor operations.</param>
public class FuturesOptionContractQueryActor(
    IDbContextFactory dbFactory,
    ILogger<FuturesOptionContractQueryActor> logger)
    : BaseQueryActor<FuturesOptionContractQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "FuturesOptionContractQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the query associated with the message.
    /// </summary>
    /// <remarks>This method validates the provided message and resolves it to a specific query based on the
    /// message subject. The resolved query is then stored in the provided context along with additional message
    /// information.</remarks>
    /// <param name="context">The query actor context used to store message-related information. This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <param name="message">The actor message to parse. This parameter cannot be <see langword="null"/>.</param>
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
    /// <remarks>This dictionary enables efficient dispatching and parsing of incoming NATS messages based on
    /// their verb. Each entry associates a specific query verb with a function that converts a NATS message payload
    /// into a strongly typed query object implementing the IQuery interface. The mapping is intended for internal
    /// use in query deserialization and routing scenarios.</remarks>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IQuery>> _parseMap = new()
    {
        [GetFuturesOptionContractQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>()!,
        [GetFuturesOptionContractsQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractsQuery, FuturesOptionContractReadModel[]>()!,
        [GetFuturesOptionContractIdsQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractIdsQuery, string[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the futures option contract query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="state">The current state of the actor, which must be of type <see cref="FuturesOptionContractQueryState"/>.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(context, dbFactory, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures option contract query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of futures option contract-related queries by associating each query
    /// type name with a function that processes the query against a FuturesOptionContractQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesOptionContractQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = (q as GetFuturesOptionContractQuery)!;
            await query.GetFuturesOptionContractAsync(ctx, dbFactory);
        },
        [typeof(GetFuturesOptionContractsQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = (q as GetFuturesOptionContractsQuery)!;
            await query.GetFuturesOptionContractsAsync(ctx, dbFactory);
        },
        [typeof(GetFuturesOptionContractIdsQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = (q as GetFuturesOptionContractIdsQuery)!;
            await query.GetFuturesOptionContractIdsAsync(ctx, dbFactory);
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <remarks>This method attempts to handle the exception by determining the type of query that caused it
    /// and sending an appropriate error response back to the caller. If the query type is not recognized, a generic
    /// error response is sent.</remarks>
    /// <param name="context">The context in which the query is being processed. Provides access to actor-specific operations and state.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that encountered the exception.</param>
    /// <param name="verb">The verb associated with the query that caused the exception.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(query);
            IsArgumentNull.Check(verb);
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetFuturesOptionContractQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesOptionContractReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetFuturesOptionContractsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesOptionContractReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetFuturesOptionContractIdsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<string[]>(query.ErrorCode, ex.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            try { await context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, innerEx.Message)); } catch { }
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}
