using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures tick data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesTickDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures tick data lookups such as retrieving the most recent futures tick data by contract/value date
/// or by contract/tick date.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesTickDataQueryActor(IQueryActorContext<FuturesTickDataQueryActor> actorContext)
    : BaseQueryActor<FuturesTickDataQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesTickDataQuery";

    /// <summary>Gets the typed query context supplied at construction.</summary>
    protected IFuturesTickDataQueryContext QueryContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesTickDataQueryContext, nameof(actorContext))!;

    readonly ILogger<FuturesTickDataQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesTickDataQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetLastFuturesTickDataQuery.Verb] = msg => msg.AsQuery<GetLastFuturesTickDataQuery, FuturesTickDataV2ReadModel>()!,
        [GetLastFuturesTickDataByTickDateQuery.Verb] = msg => msg.AsQuery<GetLastFuturesTickDataByTickDateQuery, FuturesTickDataV2ReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesTickDataQueryActor> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(QueryContext, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures tick data query
    /// logic against the query state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<IFuturesTickDataQueryContext, IQuery, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesTickDataQueryContext, IQuery, ValueTask>>()
    {
        [typeof(GetLastFuturesTickDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetLastFuturesTickDataQuery)!;
            var result = await query.GetLastFuturesTickDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTickDataQuery.Verb,
                new ServiceResult<FuturesTickDataV2ReadModel?>(result));
        },
        [typeof(GetLastFuturesTickDataByTickDateQuery)] = async (ctx, q) =>
        {
            var query = (q as GetLastFuturesTickDataByTickDateQuery)!;
            var result = await query.GetLastFuturesTickDataByTickDateAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTickDataByTickDateQuery.Verb,
                new ServiceResult<FuturesTickDataV2ReadModel?>(result));
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
        IQueryActorContext<FuturesTickDataQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
