using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing market data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="MarketDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to market data lookups such as rates of return, trading days/dates and value dates.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class MarketDataQueryActor(
    IDbContextFactory dbFactory,
    ILogger<MarketDataQueryActor> logger)
    : BaseQueryActor<MarketDataQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "MarketDataQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <remarks>This method validates the provided message and resolves it to a specific query based on the
    /// message subject. The resolved query is then stored in the provided context along with additional message
    /// information.</remarks>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        IQuery? query = msgSubject.Verb switch
        {
            GetLastRateOfReturnQuery.Verb => message.AsQuery<GetLastRateOfReturnQuery, RateOfReturnReadModel>(),
            GetTradingDaysQuery.Verb => message.AsQuery<GetTradingDaysQuery, ScalarReadModel<int>>(),
            GetTradingDatesQuery.Verb => message.AsQuery<GetTradingDatesQuery, DateOnly[]>(),
            GetValueDateQuery.Verb => message.AsQuery<GetValueDateQuery, ScalarReadModel<DateOnly>>(),
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}")
        };
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            verb: msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

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
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        return query switch
        {
            GetLastRateOfReturnQuery typedQuery => ReceiveAsync(context, typedQuery, cancellationToken),
            GetTradingDaysQuery typedQuery => ReceiveAsync(context, typedQuery, cancellationToken),
            GetTradingDatesQuery typedQuery => ReceiveAsync(context, typedQuery, cancellationToken),
            GetValueDateQuery typedQuery => ReceiveAsync(context, typedQuery, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unable to process {ActorName} query: {query.GetType().Name}")
        };
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetLastRateOfReturnQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetLastRateOfReturnAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetLastRateOfReturnQuery.Verb,
            new ServiceResult<RateOfReturnReadModel>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetTradingDaysQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetTradingDaysAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetTradingDaysQuery.Verb,
            new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetTradingDatesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetTradingDatesAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetTradingDatesQuery.Verb,
            new ServiceResult<DateOnly[]>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetValueDateQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetValueDateAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetValueDateQuery.Verb,
            new ServiceResult<ScalarReadModel<DateOnly>>(result)).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that caused the exception.</param>
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(ex?.Message);

        try
        {
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetLastRateOfReturnQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<RateOfReturnReadModel?>(query.ErrorCode, ex.Message)),
                _ when query is GetTradingDaysQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarReadModel<int>>(query.ErrorCode, ex.Message)),
                _ when query is GetTradingDatesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<DateOnly[]>(query.ErrorCode, ex.Message)),
                _ when query is GetValueDateQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarReadModel<DateOnly>>(query.ErrorCode, ex.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }

}
