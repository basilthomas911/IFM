using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

/// <summary>
/// Represents an actor responsible for managing yield curve rate queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="YieldCurveRateQueryActor"/> is a specialized query actor designed to handle operations
/// related to yield curve rates. It processes queries such as retrieving yield curve rates by date range, checking existence,
/// and fetching external yield curve rate data. This actor uses dependency injection to resolve required services.</remarks>
/// <param name="logger">The logger instance for tracking actor operations.</param>
public class YieldCurveRateQueryActor(
    IDbContextFactory dbFactory,
    ILogger<YieldCurveRateQueryActor> logger)
    : BaseQueryActor<YieldCurveRateQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "YieldCurveRateQuery";

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
        [GetLastYieldCurveRateQuery.Verb] = msg => msg.AsQuery<GetLastYieldCurveRateQuery, YieldCurveRateReadModel>()!,
        [GetYieldCurveRatesQuery.Verb] = msg => msg.AsQuery<GetYieldCurveRatesQuery, YieldCurveRateReadModel[]>()!,
        [GetYieldCurveRateExistsQuery.Verb] = msg => msg.AsQuery<GetYieldCurveRateExistsQuery, ScalarReadModel<bool>>()!,
        [GetYieldCurveRateYearsQuery.Verb] = msg => msg.AsQuery<GetYieldCurveRateYearsQuery, YieldCurveRateYearsReadModel>()!,
        [GetExternalYieldCurveRatesQuery.Verb] = msg => msg.AsQuery<GetExternalYieldCurveRatesQuery, YieldCurveRateReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the yield curve rate query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
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
    /// Provides a mapping from query type names to delegate functions that execute the corresponding yield curve rate query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of yield curve rate-related queries by associating each query
    /// type name with a function that processes the query against a YieldCurveRateQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetLastYieldCurveRateQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetLastYieldCurveRateQuery;
            await query.GetLastYieldCurveRateAsync(ctx, dbFactory);
        },
        [typeof(GetYieldCurveRatesQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetYieldCurveRatesQuery;
            await query.GetYieldCurveRatesAsync(ctx, dbFactory);
        },
        [typeof(GetYieldCurveRateExistsQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetYieldCurveRateExistsQuery;
            await query.GetYieldCurveRateExistsAsync(ctx, dbFactory);
        },
        [typeof(GetYieldCurveRateYearsQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetYieldCurveRateYearsQuery;
            await query.GetYieldCurveRateYearsAsync(ctx, dbFactory);
        },
        [typeof(GetExternalYieldCurveRatesQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetExternalYieldCurveRatesQuery;
            await query.GetExternalYieldCurveRatesAsync(ctx, dbFactory);
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
                _ when query is GetLastYieldCurveRateQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<YieldCurveRateReadModel?>(query.ErrorCode, ex.Message)),
                _ when query is GetYieldCurveRatesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<YieldCurveRateReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetYieldCurveRateExistsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarReadModel<bool>>(query.ErrorCode, ex.Message)),
                _ when query is GetYieldCurveRateYearsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<YieldCurveRateYearsReadModel>(query.ErrorCode, ex.Message)),
                _ when query is GetExternalYieldCurveRatesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<YieldCurveRateReadModel[]>(query.ErrorCode, ex.Message)),
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
