using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;

public class MarketDataFeedQueryActor(IQueryActorContext<MarketDataFeedQueryActor> actorContext)
    : BaseQueryActor<MarketDataFeedQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketDataFeedQuery";

    /// <summary>Gets the typed query context supplied at construction.</summary>
    protected IMarketDataFeedQueryContext QueryContext { get; } =
        IsArgumentNull.Set(actorContext as IMarketDataFeedQueryContext, nameof(actorContext))!;

    readonly ILogger<MarketDataFeedQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly MarketDataFeedQueryParameters _qryParameters = new(
        ((IMarketDataFeedQueryContext)actorContext).MarketDataApi, ((IMarketDataFeedQueryContext)actorContext).SequenceIdGenerator, ((IMarketDataFeedQueryContext)actorContext).DbFactory);

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
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesOptionContractQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>()!,
        [GetFuturesOptionSpreadDataQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionSpreadDataQuery, FuturesOptionSpreadDataReadModel>()!,
        [GetFuturesRiskPositionTypeQuery.Verb] = msg => msg.AsQuery<GetFuturesRiskPositionTypeQuery, RiskPositionTypeReadModel>()!,
        [GetIronCondorMarketDataFeedQuery.Verb] = msg => msg.AsQuery<GetIronCondorMarketDataFeedQuery, IronCondorMarketDataFeedReadModel>()!,
        [GetNormalCurveTableQuery.Verb] = msg => msg.AsQuery<GetNormalCurveTableQuery, NormalCurveTableReadModel>()!,
        [GetMarketDataFeedRuntimeStatusQuery.Verb] = msg => msg.AsQuery<GetMarketDataFeedRuntimeStatusQuery, MarketDataFeedRuntimeStatusReadModel>()!,
        [GetStreamingRequestIdQuery.Verb] = msg => msg.AsQuery<GetStreamingRequestIdQuery, ScalarValue<int>>()!
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
    static readonly Dictionary<Type, Func<IMarketDataFeedQueryContext, MarketDataFeedQueryParameters, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesOptionContractQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesOptionContractQuery)!;
            var result = await query.GetFuturesOptionContractFromProviderAsync(qryParams.MarketDataApi);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractQuery.Verb,
                new ServiceResult<FuturesOptionContractReadModel>(result));
        },
        [typeof(GetFuturesOptionSpreadDataQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesOptionSpreadDataQuery)!;
            var result = await query.GetFuturesOptionSpreadDataAsync(qryParams.MarketDataApi);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionSpreadDataQuery.Verb,
                new ServiceResult<FuturesOptionSpreadDataReadModel>(result));
        },
        [typeof(GetFuturesRiskPositionTypeQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesRiskPositionTypeQuery)!;
            var result = await query.GetFuturesRiskPositionTypeAsync(qryParams.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesRiskPositionTypeQuery.Verb,
                new ServiceResult<RiskPositionTypeReadModel>(result));
        },
        [typeof(GetIronCondorMarketDataFeedQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetIronCondorMarketDataFeedQuery)!;
            var result = await query.GetIronCondorMarketDataFeedAsync(qryParams.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetIronCondorMarketDataFeedQuery.Verb,
                new ServiceResult<IronCondorMarketDataFeedReadModel>(result));
        },
        [typeof(GetNormalCurveTableQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetNormalCurveTableQuery)!;
            var result = await query.GetNormalCurveTableAsync(qryParams.DbFactory);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetNormalCurveTableQuery.Verb,
                new ServiceResult<NormalCurveTableReadModel>(result));
        },
        [typeof(GetMarketDataFeedRuntimeStatusQuery)] = async (ctx, qryParams, q) =>
        {
            var result = qryParams.MarketDataApi.GetRuntimeStatus();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMarketDataFeedRuntimeStatusQuery.Verb,
                new ServiceResult<MarketDataFeedRuntimeStatusReadModel>(result));
        },
        [typeof(GetStreamingRequestIdQuery)] = async (ctx, qryParams, q) =>
        {
            var query = (q as GetStreamingRequestIdQuery)!;
            var result = await query.GetStreamingRequestIdAsync(qryParams.SequenceIdGenerator);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetStreamingRequestIdQuery.Verb,
                new ServiceResult<ScalarValue<int>>(result));
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
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
