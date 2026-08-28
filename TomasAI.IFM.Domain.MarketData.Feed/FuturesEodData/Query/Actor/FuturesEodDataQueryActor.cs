using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures EOD data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesEodDataQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures end-of-day data lookups such as retrieving EOD data by date range, EOD data parameters,
/// EOD data by contract/date, last EOD data, moving averages, and VIX futures EOD data.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="dbFactory">The database context factory used to access market data.</param>
/// <param name="logger"></param>
public class FuturesEodDataQueryActor(IQueryActorContext<FuturesEodDataQueryActor> actorContext)
    : BaseQueryActor<FuturesEodDataQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesEodDataQuery";

    /// <summary>Gets the typed query context supplied at construction.</summary>
    protected IFuturesEodDataQueryContext QueryContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesEodDataQueryContext, nameof(actorContext))!;

    readonly ILogger<FuturesEodDataQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesEodDataQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesEodDataByDateRangeQuery.Verb] = msg => msg.AsQuery<GetFuturesEodDataByDateRangeQuery, FuturesEodDataV2ReadModel[]>()!,
        [GetFuturesEodDataParametersQuery.Verb] = msg => msg.AsQuery<GetFuturesEodDataParametersQuery, FuturesEodDataParametersReadModel>()!,
        [GetFuturesEodDataQuery.Verb] = msg => msg.AsQuery<GetFuturesEodDataQuery, FuturesEodDataV2ReadModel>()!,
        [GetLastFuturesEodDataQuery.Verb] = msg => msg.AsQuery<GetLastFuturesEodDataQuery, FuturesEodDataV2ReadModel>()!,
        [GetFuturesEodDataMovingAveragesQuery.Verb] = msg => msg.AsQuery<GetFuturesEodDataMovingAveragesQuery, FuturesEodDataMovingAveragesReadModel>()!,
        [GetLastVixFuturesEodDataQuery.Verb] = msg => msg.AsQuery<GetLastVixFuturesEodDataQuery, VixFuturesEodDataReadModel>()!,
        [GetVixFuturesEodDataQuery.Verb] = msg => msg.AsQuery<GetVixFuturesEodDataQuery, VixFuturesEodDataReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<FuturesEodDataQueryActor> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(QueryContext, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures EOD data query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<Type, Func<IFuturesEodDataQueryContext, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesEodDataByDateRangeQuery)] = async (ctx, q) =>
        {
            var query = (q as GetFuturesEodDataByDateRangeQuery)!;
            var result = await query.GetFuturesEodDataByDateRangeAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataByDateRangeQuery.Verb,
                new ServiceResult<FuturesEodDataV2ReadModel[]>(result));
        },
        [typeof(GetFuturesEodDataParametersQuery)] = async (ctx, q) =>
        {
            var query = (q as GetFuturesEodDataParametersQuery)!;
            var result = await query.GetFuturesEodDataParametersAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataParametersQuery.Verb,
                new ServiceResult<FuturesEodDataParametersReadModel>(result));
        },
        [typeof(GetFuturesEodDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetFuturesEodDataQuery)!;
            var result = await query.GetFuturesEodDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataQuery.Verb,
                new ServiceResult<FuturesEodDataV2ReadModel>(result));
        },
        [typeof(GetLastFuturesEodDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetLastFuturesEodDataQuery)!;
            var result = await query.GetLastFuturesEodDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesEodDataQuery.Verb,
                new ServiceResult<FuturesEodDataV2ReadModel>(result));
        },
        [typeof(GetFuturesEodDataMovingAveragesQuery)] = async (ctx, q) =>
        {
            var query = (q as GetFuturesEodDataMovingAveragesQuery)!;
            var result = await query.GetFuturesEodMovingAveragesAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataMovingAveragesQuery.Verb,
                new ServiceResult<FuturesEodDataMovingAveragesReadModel>(result));
        },
        [typeof(GetLastVixFuturesEodDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetLastVixFuturesEodDataQuery)!;
            var result = await query.GetLastVixFuturesEodDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastVixFuturesEodDataQuery.Verb,
                new ServiceResult<VixFuturesEodDataReadModel?>(result));
        },
        [typeof(GetVixFuturesEodDataQuery)] = async (ctx, q) =>
        {
            var query = (q as GetVixFuturesEodDataQuery)!;
            var result = await query.GetVixFuturesEodDataAsync(ctx.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetVixFuturesEodDataQuery.Verb,
                new ServiceResult<VixFuturesEodDataReadModel[]>(result));
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
        IQueryActorContext<FuturesEodDataQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
