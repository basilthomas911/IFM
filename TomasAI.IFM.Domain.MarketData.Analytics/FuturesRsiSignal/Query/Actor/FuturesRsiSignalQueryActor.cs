using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures RSI signal queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesRsiSignalQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures RSI signal lookups such as retrieving the last RSI signal and trend direction from RSI signals.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesRsiSignalQueryActor(
    IQueryActorContext<FuturesRsiSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesRsiSignalQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesRsiSignalQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesRsiSignalQueryContext, nameof(Context))!;

    public const string ActorName = "FuturesRsiSignalQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesRsiSignalQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetFuturesRsiSignalQuery.Verb] = msg => msg.AsQuery<GetFuturesRsiSignalQuery, FuturesRsiSignalReadModel>()!,
        [GetFuturesRsiDailySignalQuery.Verb] = msg => msg.AsQuery<GetFuturesRsiDailySignalQuery, FuturesRsiSignalReadModel>()!,
        [GetFuturesTrendDirectionFromRSISignalQuery.Verb] = msg =>
            msg.AsQuery<GetFuturesTrendDirectionFromRSISignalQuery, FuturesTrendDirectionReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesRsiSignalQueryActor> context, IQuery query)
        => await ReceiveAsync(context, query, CancellationToken.None).ConfigureAwait(false);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesRsiSignalQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receive = ResolveMappedQueryHandler(query, _receiveMap);
        await receive(dispatchContext, ActorContext.DbFactory, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures RSI signal query
    /// logic against the query state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<FuturesRsiSignalQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<FuturesRsiSignalQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetFuturesRsiSignalQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetFuturesRsiSignalQuery)!;
            var result = await query.GetLastFuturesRsiSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesRsiSignalReadModel?>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesRsiSignalQuery.Verb, serviceResult).ConfigureAwait(false);

        },
        [typeof(GetFuturesRsiDailySignalQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetFuturesRsiDailySignalQuery)!;
            var result = await query.GetLastFuturesRsiDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesRsiSignalReadModel?>(result);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesRsiDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
        },
        [typeof(GetFuturesTrendDirectionFromRSISignalQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (GetFuturesTrendDirectionFromRSISignalQuery)q;
            var result = await query.GetFuturesTrendDirectionAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesTrendDirectionReadModel>(result);
            await ctx.ReplyAsync(
                q.Subject.ThreadId,
                GetFuturesTrendDirectionFromRSISignalQuery.Verb,
                serviceResult).ConfigureAwait(false);
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
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesRsiSignalQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

}
