using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures ADX signal queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesAdxSignalQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures ADX signal lookups such as retrieving the last ADX signal. It processes queries, validates them,
/// and manages the actor's state.</remarks>
/// <param name="logger">The logger used to record diagnostic and operational information.</param>
public class FuturesAdxSignalQueryActor(
    IQueryActorContext<FuturesAdxSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesAdxSignalQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesAdxSignalQueryContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesAdxSignalQueryContext, nameof(Context))!;

    public const string ActorName = "FuturesAdxSignalQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesAdxSignalQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesAdxSignalQuery.Verb] = msg => msg.AsQuery<GetFuturesAdxSignalQuery, FuturesAdxSignalReadModel>()!,
        [GetFuturesAdxDailySignalQuery.Verb] = msg => msg.AsQuery<GetFuturesAdxDailySignalQuery, FuturesAdxSignalReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesAdxSignalQueryActor> context, IQuery query)
        => await ReceiveAsync(context, query, CancellationToken.None).ConfigureAwait(false);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesAdxSignalQueryActor> context,
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
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures ADX signal query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<Type, Func<IQueryActorContext<FuturesAdxSignalQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesAdxSignalQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetFuturesAdxSignalQuery)!;
            var queryResult = await query.GetLastFuturesAdxSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesAdxSignalReadModel?>(queryResult);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAdxSignalQuery.Verb, serviceResult).ConfigureAwait(false);
        },
        [typeof(GetFuturesAdxDailySignalQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetFuturesAdxDailySignalQuery)!;
            var queryResult = await query.GetLastFuturesAdxDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serviceResult = new ServiceResult<FuturesAdxSignalReadModel?>(queryResult);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAdxDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
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
        IQueryActorContext<FuturesAdxSignalQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
