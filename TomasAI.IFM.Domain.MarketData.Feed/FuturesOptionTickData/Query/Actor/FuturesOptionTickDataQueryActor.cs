using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures option tick data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesOptionTickDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures option tick data lookups such as retrieving the most recent tick data for a contract and date.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger"></param>
public class FuturesOptionTickDataQueryActor(IQueryActorContext<FuturesOptionTickDataQueryActor> actorContext)
    : BaseQueryActor<FuturesOptionTickDataQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesOptionTickDataQuery";

    /// <summary>Gets the typed query context supplied at construction.</summary>
    protected IFuturesOptionTickDataQueryContext QueryContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesOptionTickDataQueryContext, nameof(actorContext))!;

    readonly ILogger<FuturesOptionTickDataQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesOptionTickDataQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetLastFuturesOptionTickDataQuery.Verb] = msg => msg.AsQuery<GetLastFuturesOptionTickDataQuery, FuturesOptionTickDataV2ReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionTickDataQueryActor> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(QueryContext, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures option tick data query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<Type, Func<IFuturesOptionTickDataQueryContext, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetLastFuturesOptionTickDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetLastFuturesOptionTickDataQuery)!;
            var result = await query.GetLastFuturesOptionTickDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesOptionTickDataQuery.Verb,
                new ServiceResult<FuturesOptionTickDataV2ReadModel?>(result));
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
        IQueryActorContext<FuturesOptionTickDataQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
