using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing yield curve rate queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="YieldCurveRateQueryActor"/> is a specialized query actor designed to handle operations
/// related to yield curve rates. It processes queries such as retrieving yield curve rates by date range, checking existence,
/// and retrieving persisted yield curve data. This actor uses dependency injection to resolve required services.</remarks>
/// <param name="logger">The logger instance for tracking actor operations.</param>
public class YieldCurveRateQueryActor(IQueryActorContext<YieldCurveRateQueryActor> actorContext)
    : BaseQueryActor<YieldCurveRateQueryActor>(actorContext.Logger, actorContext.ActorId)
{
    public const string ActorName = "YieldCurveRateQuery";
    readonly ILogger<YieldCurveRateQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    protected IYieldCurveRateQueryContext YieldCurveRateContext { get; } =
        IsArgumentNull.Set(actorContext as IYieldCurveRateQueryContext, nameof(actorContext))!;

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
    protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName })
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        IQuery? query = msgSubject.Verb switch
        {
            GetLastYieldCurveRateQuery.Verb => message.AsQuery<GetLastYieldCurveRateQuery, YieldCurveRateReadModel>(),
            GetYieldCurveRatesQuery.Verb => message.AsQuery<GetYieldCurveRatesQuery, YieldCurveRateReadModel[]>(),
            GetYieldCurveRateExistsQuery.Verb => message.AsQuery<GetYieldCurveRateExistsQuery, ScalarReadModel<bool>>(),
            GetYieldCurveRateYearsQuery.Verb => message.AsQuery<GetYieldCurveRateYearsQuery, YieldCurveRateYearsReadModel>(),
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
    /// <remarks>This method uses typed dispatch so unsupported query types fail immediately.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        using var messageInfoScope = context.MirrorMessageInfoTo(YieldCurveRateContext, query.Subject.ThreadId, query.Subject.Verb);
        await (query switch
        {
            GetLastYieldCurveRateQuery typedQuery => ReceiveAsync(YieldCurveRateContext, typedQuery, cancellationToken),
            GetYieldCurveRatesQuery typedQuery => ReceiveAsync(YieldCurveRateContext, typedQuery, cancellationToken),
            GetYieldCurveRateExistsQuery typedQuery => ReceiveAsync(YieldCurveRateContext, typedQuery, cancellationToken),
            GetYieldCurveRateYearsQuery typedQuery => ReceiveAsync(YieldCurveRateContext, typedQuery, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unable to process {ActorName} query: {query.GetType().Name}")
        });
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetLastYieldCurveRateQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetLastYieldCurveRateAsync(YieldCurveRateContext.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetLastYieldCurveRateQuery.Verb,
            new ServiceResult<YieldCurveRateReadModel?>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetYieldCurveRatesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetYieldCurveRatesAsync(YieldCurveRateContext.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetYieldCurveRatesQuery.Verb,
            new ServiceResult<YieldCurveRateReadModel[]>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetYieldCurveRateExistsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetYieldCurveRateExistsAsync(YieldCurveRateContext.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetYieldCurveRateExistsQuery.Verb,
            new ServiceResult<ScalarReadModel<bool>>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext context,
        GetYieldCurveRateYearsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetYieldCurveRateYearsAsync(YieldCurveRateContext.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetYieldCurveRateYearsQuery.Verb,
            new ServiceResult<YieldCurveRateYearsReadModel>(result)).ConfigureAwait(false);
    }

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
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            try { await context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, innerEx.Message)); } catch { }
            _logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
   
}
