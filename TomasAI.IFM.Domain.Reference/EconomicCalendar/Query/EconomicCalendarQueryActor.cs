using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

/// <summary>
/// Represents an actor responsible for managing economic calendar queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="EconomicCalendarQueryActor"/> is a specialized query actor designed to handle operations
/// related to economic calendar data. It processes queries such as retrieving all calendar entries, filtering by date,
/// view type, and country, and validates the queries. This actor relies on a query state for data
/// retrieval and uses dependency injection to resolve required services.</remarks>
/// <param name="logger"></param>
public class EconomicCalendarQueryActor(
    IDbContextFactory dbFactory,
    ILogger<EconomicCalendarQueryActor> logger)
    : BaseQueryActor<EconomicCalendarQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "EconomicCalendarQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <remarks>This method validates the provided message and resolves it to a specific query based on the
    /// message subject. The resolved query is then stored in the provided context along with additional message
    /// information.</remarks>
    /// <param name="message">The actor message to parse. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="context">The query actor context used to store message-related information. This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <returns>The thread identifier extracted from the message subject.</returns>
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
        [GetEconomicCalendarAllQuery.Verb] = msg => msg.AsQuery<GetEconomicCalendarAllQuery, EconomicCalendarReadModel[]>()!,
        [GetEconomicCalendarQuery.Verb] = msg => msg.AsQuery<GetEconomicCalendarQuery, EconomicCalendarReadModel[]>()!,
        [GetEconomicCalendarDateQuery.Verb] = msg => msg.AsQuery<GetEconomicCalendarDateQuery, string>()!,
        [GetEconomicCalendarCountryCodesQuery.Verb] = msg => msg.AsQuery<GetEconomicCalendarCountryCodesQuery, EconomicCalendarCountryCodeReadModel[]>()!,
        [GetExternalEconomicCalendarsQuery.Verb] = msg => msg.AsQuery<GetExternalEconomicCalendarsQuery, EconomicCalendarReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the economic calendar query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported or cannot be processed by the actor.</exception>
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
    /// Provides a mapping from query type names to delegate functions that execute the corresponding economic calendar query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of economic calendar-related queries by associating each query
    /// type name with a function that processes the query against an EconomicCalendarQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetEconomicCalendarAllQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetEconomicCalendarAllQuery;
            await query.GetEconomicCalendarAllAsync(ctx, dbFactory);
        },
        [typeof(GetEconomicCalendarQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetEconomicCalendarQuery;
            await query.GetEconomicCalendarAsync(ctx, dbFactory);
        },
        [typeof(GetEconomicCalendarDateQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetEconomicCalendarDateQuery;
            await query.GetEconomicCalendarDateAsync(ctx, dbFactory);
        },
        [typeof(GetEconomicCalendarCountryCodesQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetEconomicCalendarCountryCodesQuery;
             await query.GetEconomicCalendarCountryCodesAsync(ctx, dbFactory);
        },
        [typeof(GetExternalEconomicCalendarsQuery).Name] = async (ctx, dbFactory, q) =>
        {
            var query = q as GetExternalEconomicCalendarsQuery;
            await query.GetExternalEconomicCalendarsAsync(ctx, dbFactory);
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <remarks>This method attempts to handle the exception by determining the type of query that caused it
    /// and sending an appropriate error response back to the caller. If the query type is not recognized, an <see
    /// cref="InvalidOperationException"/> is thrown.</remarks>
    /// <param name="context">The context in which the query is being processed. Provides access to actor-specific operations and state.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that was being processed when the exception occurred.</param>
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported or cannot be processed by the actor.</exception>
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(query);
            IsArgumentNull.Check(verb);
            IsArgumentNull.Check(ex.Message);
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetEconomicCalendarAllQuery 
                    => context.ReplyAsync(threadId, verb, new ServiceResult<EconomicCalendarReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetEconomicCalendarQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<EconomicCalendarReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetEconomicCalendarDateQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<string>(query.ErrorCode, ex.Message)),
                _ when query is GetEconomicCalendarCountryCodesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<EconomicCalendarCountryCodeReadModel[]>(query.ErrorCode, ex.Message)),
                _ when query is GetExternalEconomicCalendarsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<EconomicCalendarReadModel[]>(query.ErrorCode, ex.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            try { await context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, innerEx.Message)); }
            catch { /* Swallow secondary exceptions during exception handling */ }
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}
