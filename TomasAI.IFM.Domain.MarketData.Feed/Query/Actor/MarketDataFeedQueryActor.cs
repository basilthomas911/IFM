using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;

public class MarketDataFeedQueryActor(IQueryActorContext<MarketDataFeedQueryActor> actorContext)
    : BaseQueryActor<MarketDataFeedQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketDataFeedQuery";

    /// <summary>Gets the typed query context supplied at construction.</summary>
    protected IMarketDataFeedQueryContext QueryContext { get; } =
        IsArgumentNull.Named(actorContext as IMarketDataFeedQueryContext, nameof(actorContext))!;
    readonly MarketDataFeedQueryParameters _qryParameters = new(
        ((IMarketDataFeedQueryContext)actorContext).MarketDataApi,
        ((IMarketDataFeedQueryContext)actorContext).DbFactory, ((IMarketDataFeedQueryContext)actorContext).MarketDataServiceStore,
        ((IMarketDataFeedQueryContext)actorContext).MarketDataLifecycle);

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<MarketDataFeedQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetFuturesOptionContractQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>()!,
        [GetFuturesOptionSpreadDataQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionSpreadDataQuery, FuturesOptionSpreadDataReadModel>()!,
        [GetFuturesRiskPositionTypeQuery.Verb] = msg => msg.AsQuery<GetFuturesRiskPositionTypeQuery, RiskPositionTypeReadModel>()!,
        [GetIronCondorMarketDataFeedQuery.Verb] = msg => msg.AsQuery<GetIronCondorMarketDataFeedQuery, IronCondorMarketDataFeedReadModel>()!,
        [GetNormalCurveTableQuery.Verb] = msg => msg.AsQuery<GetNormalCurveTableQuery, NormalCurveTableReadModel>()!,
        [GetMarketDataFeedRuntimeStatusQuery.Verb] = msg => msg.AsQuery<GetMarketDataFeedRuntimeStatusQuery, MarketDataFeedRuntimeStatusReadModel>()!,
        [GetDatabentoReadinessQuery.Verb] = msg => msg.AsQuery<GetDatabentoReadinessQuery, DatabentoReadinessReadModel>()!,
        [GetDatabentoCurrentContractsQuery.Verb] = msg => msg.AsQuery<GetDatabentoCurrentContractsQuery, DatabentoContractAssignmentReadModel[]>()!,
        [GetDatabentoWatchdogHistoryQuery.Verb] = msg => msg.AsQuery<GetDatabentoWatchdogHistoryQuery, DatabentoWatchdogObservationReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext<MarketDataFeedQueryActor> context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(QueryContext, _qryParameters, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding market data feed query
    /// logic against the query state.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<IMarketDataFeedQueryContext, MarketDataFeedQueryParameters, IQuery, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IMarketDataFeedQueryContext, MarketDataFeedQueryParameters, IQuery, ValueTask>>
        {
            [typeof(GetFuturesOptionContractQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetFuturesOptionContractQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetFuturesOptionContractQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetFuturesOptionSpreadDataQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetFuturesOptionSpreadDataQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetFuturesOptionSpreadDataQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetFuturesRiskPositionTypeQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetFuturesRiskPositionTypeQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetFuturesRiskPositionTypeQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetIronCondorMarketDataFeedQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetIronCondorMarketDataFeedQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetIronCondorMarketDataFeedQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetNormalCurveTableQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetNormalCurveTableQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetNormalCurveTableQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetMarketDataFeedRuntimeStatusQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetMarketDataFeedRuntimeStatusQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetMarketDataFeedRuntimeStatusQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetDatabentoReadinessQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetDatabentoReadinessQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetDatabentoReadinessQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetDatabentoCurrentContractsQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetDatabentoCurrentContractsQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetDatabentoCurrentContractsQuery.Verb, result).ConfigureAwait(false);
            },
            [typeof(GetDatabentoWatchdogHistoryQuery)] = static async (context, parameters, query) =>
            {
                var result = await ((GetDatabentoWatchdogHistoryQuery)query).ExecuteAsync(context, parameters).ConfigureAwait(false);
                await context.ReplyAsync(query.Subject.ThreadId, GetDatabentoWatchdogHistoryQuery.Verb, result).ConfigureAwait(false);
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
        IQueryActorContext<MarketDataFeedQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
