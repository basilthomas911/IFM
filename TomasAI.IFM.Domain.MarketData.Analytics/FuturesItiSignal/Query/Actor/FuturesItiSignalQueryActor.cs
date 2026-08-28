using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures ITI signal queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesItiSignalQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures intrinsic time indicator signal lookups such as retrieving signal data, MDI data,
/// MDI data by trend, last signal, and trend direction changed signals.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesItiSignalQueryActor(
    IQueryActorContext<FuturesItiSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesItiSignalQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesItiSignalQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesItiSignalQueryContext, nameof(Context))!;

    public const string ActorName = "FuturesItiSignalQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesItiSignalQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesItiSignalDataQuery.Verb] = msg => msg.AsQuery<GetFuturesItiSignalDataQuery, FuturesItiSignalDataReadModel>()!,
        [GetFuturesItiSignalHistoryQuery.Verb] = msg => msg.AsQuery<GetFuturesItiSignalHistoryQuery, FuturesItiSignalV2ReadModel[]>()!,
        [GetFuturesItiSignalQuery.Verb] = msg => msg.AsQuery<GetFuturesItiSignalQuery, FuturesItiSignalV2ReadModel>()!,
        [GetFuturesItiTrendDirectionChangedSignalsQuery.Verb] = msg => msg.AsQuery<GetFuturesItiTrendDirectionChangedSignalsQuery, FuturesItiSignalV2ReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesItiSignalQueryActor> context, IQuery query)
        => await ReceiveAsync(context, query, CancellationToken.None).ConfigureAwait(false);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesItiSignalQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(dispatchContext, ActorContext.DbFactory, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures ITI signal query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<Type, Func<IQueryActorContext<FuturesItiSignalQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesItiSignalDataQuery)] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesItiSignalDataQuery)!;
            var result = await query.GetFuturesItiSignalDataAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalDataQuery.Verb,
                new ServiceResult<FuturesItiSignalDataReadModel>(result)).ConfigureAwait(false);
        },
        [typeof(GetFuturesItiSignalQuery)] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesItiSignalQuery)!;
            var result = await query.GetLastFuturesItiSignalAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalQuery.Verb,
                new ServiceResult<FuturesItiSignalV2ReadModel?>(result)).ConfigureAwait(false);
        },
        [typeof(GetFuturesItiSignalHistoryQuery)] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesItiSignalHistoryQuery)!;
            var result = await query.GetFuturesItiSignalHistoryAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalHistoryQuery.Verb,
                new ServiceResult<FuturesItiSignalV2ReadModel[]>(result)).ConfigureAwait(false);
        },
        [typeof(GetFuturesItiTrendDirectionChangedSignalsQuery)] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesItiTrendDirectionChangedSignalsQuery)!;
            var result = await query.GetFuturesItiTrendDirectionChangedSignalsAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb,
                new ServiceResult<FuturesItiSignalV2ReadModel[]>(result)).ConfigureAwait(false);
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
        IQueryActorContext<FuturesItiSignalQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
