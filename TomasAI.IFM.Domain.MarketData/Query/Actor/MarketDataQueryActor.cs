using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing market data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="MarketDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to market data lookups such as rates of return, trading days/dates and value dates.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class MarketDataQueryActor(IQueryActorContext<MarketDataQueryActor> actorContext)
    : BaseQueryActor<MarketDataQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketDataQuery";
    readonly ILogger<MarketDataQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    protected IMarketDataQueryContext MarketDataContext { get; } =
        IsArgumentNull.Set(actorContext as IMarketDataQueryContext, nameof(actorContext))!;

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
    protected override IQuery ParseMessage(IQueryActorContext<MarketDataQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetLastRateOfReturnQuery.Verb] = message =>
            message.AsQuery<GetLastRateOfReturnQuery, RateOfReturnReadModel>()!,
        [GetTradingDaysQuery.Verb] = message =>
            message.AsQuery<GetTradingDaysQuery, ScalarReadModel<int>>()!,
        [GetTradingDatesQuery.Verb] = message =>
            message.AsQuery<GetTradingDatesQuery, DateOnly[]>()!,
        [GetValueDateQuery.Verb] = message =>
            message.AsQuery<GetValueDateQuery, ScalarReadModel<DateOnly>>()!,
        [GetMarketSessionQuery.Verb] = message =>
            message.AsQuery<GetMarketSessionQuery, MarketSessionReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<MarketDataQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(this, MarketDataContext, query, cancellationToken).ConfigureAwait(false);
    }

    static readonly Dictionary<Type,
        Func<MarketDataQueryActor, IMarketDataQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetLastRateOfReturnQuery)] = static (actor, context, query, cancellationToken) =>
            actor.ReceiveAsync(context, (GetLastRateOfReturnQuery)query, cancellationToken),
        [typeof(GetTradingDaysQuery)] = static (actor, context, query, cancellationToken) =>
            actor.ReceiveAsync(context, (GetTradingDaysQuery)query, cancellationToken),
        [typeof(GetTradingDatesQuery)] = static (actor, context, query, cancellationToken) =>
            actor.ReceiveAsync(context, (GetTradingDatesQuery)query, cancellationToken),
        [typeof(GetValueDateQuery)] = static (actor, context, query, cancellationToken) =>
            actor.ReceiveAsync(context, (GetValueDateQuery)query, cancellationToken),
        [typeof(GetMarketSessionQuery)] = static (actor, context, query, cancellationToken) =>
            actor.ReceiveAsync(context, (GetMarketSessionQuery)query, cancellationToken)
    };

    async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        GetLastRateOfReturnQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetLastRateOfReturnAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetLastRateOfReturnQuery.Verb,
            new ServiceResult<RateOfReturnReadModel>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        GetTradingDaysQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetTradingDaysAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetTradingDaysQuery.Verb,
            new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        GetTradingDatesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetTradingDatesAsync(context.DbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetTradingDatesQuery.Verb,
            new ServiceResult<DateOnly[]>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        GetValueDateQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetValueDateAsync(
            MarketDataContext.MarketSessionAuthority,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetValueDateQuery.Verb,
            new ServiceResult<ScalarReadModel<DateOnly>>(result)).ConfigureAwait(false);
    }

    async ValueTask ReceiveAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        GetMarketSessionQuery query,
        CancellationToken cancellationToken)
    {
        var result = await query.GetMarketSessionAsync(
            MarketDataContext.MarketSessionAuthority,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, GetMarketSessionQuery.Verb,
            new ServiceResult<MarketSessionReadModel>(result)).ConfigureAwait(false);
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
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<MarketDataQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

}
