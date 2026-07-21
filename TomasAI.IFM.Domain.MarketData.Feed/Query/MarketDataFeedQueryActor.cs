using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public class MarketDataFeedQueryActor(
    IMarketDataSnapshotApi marketDataSnapshotApi,
    IBlackboardService blackboardService,
    IDbContextFactory dbFactory,
    ILogger<MarketDataFeedQueryActor> logger)
    : BaseQueryActor<MarketDataFeedQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "MarketDataFeedQuery";
    readonly MarketDataFeedQueryParameters _qryParameters = new(
        marketDataSnapshotApi, blackboardService, dbFactory);

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Query, Name: ActorName }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            throw new InvalidOperationException($"Unable to resolve {ActorName} query from message: {message.Subject}");
        var query = messageParser.Invoke(message);
        IsArgumentNull.Check(query);
        context.SetMessageInfo(
            msgSubject.ThreadId,
            verb: msgSubject.Verb,
            new ActorMessageInfo(message, query));
        return query;
    }

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IQuery>> _parseMap = new()
    {
        [GetFuturesOptionContractQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>()!,
        [GetFuturesOptionSpreadDataQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionSpreadDataQuery, FuturesOptionSpreadDataReadModel>()!,
        [GetFuturesRiskPositionTypeQuery.Verb] = msg => msg.AsQuery<GetFuturesRiskPositionTypeQuery, RiskPositionTypeReadModel>()!,
        [GetIronCondorMarketDataFeedQuery.Verb] = msg => msg.AsQuery<GetIronCondorMarketDataFeedQuery, IronCondorMarketDataFeedReadModel>()!,
        [GetNormalCurveTableQuery.Verb] = msg => msg.AsQuery<GetNormalCurveTableQuery, NormalCurveTableReadModel>()!,
        [GetOptionQuoteIdQuery.Verb] = msg => msg.AsQuery<GetOptionQuoteIdQuery, ScalarValue<int>>()!,
        [GetStreamingRequestIdQuery.Verb] = msg => msg.AsQuery<GetStreamingRequestIdQuery, ScalarValue<int>>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(context, _qryParameters, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding market data feed query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, MarketDataFeedQueryParameters, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetFuturesOptionContractQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesOptionContractQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetFuturesOptionContractQuery.Verb);
            return query.GetFuturesOptionContractFromBrokerAsync(ctx, qryParams.MarketDataSnapshotApi);
        },
        [typeof(GetFuturesOptionSpreadDataQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesOptionSpreadDataQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetFuturesOptionSpreadDataQuery.Verb);
            return query.GetFuturesOptionSpreadDataAsync(ctx, qryParams.MarketDataSnapshotApi);
        },
        [typeof(GetFuturesRiskPositionTypeQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetFuturesRiskPositionTypeQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetFuturesRiskPositionTypeQuery.Verb);
            return query.GetFuturesRiskPositionTypeAsync(ctx, qryParams.DbFactory);
        },
        [typeof(GetIronCondorMarketDataFeedQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetIronCondorMarketDataFeedQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetIronCondorMarketDataFeedQuery.Verb);
            return query.GetIronCondorMarketDataFeedAsync(ctx, qryParams.DbFactory);
        },
        [typeof(GetNormalCurveTableQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetNormalCurveTableQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetNormalCurveTableQuery.Verb);
            return query.GetNormalCurveTableAsync(ctx, qryParams.DbFactory);
        },
        [typeof(GetOptionQuoteIdQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetOptionQuoteIdQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetOptionQuoteIdQuery.Verb);
            return query.GetOptionQuoteIdAsync(ctx, qryParams.BlackboardService.SequenceCounter);
        },
        [typeof(GetStreamingRequestIdQuery).Name] = (ctx, qryParams, q) =>
        {
            var query = (q as GetStreamingRequestIdQuery)!;
            var msgInfo = ctx.GetMessageInfo(query.Subject.ThreadId, GetStreamingRequestIdQuery.Verb);
            return query.GetStreamingRequestIdAsync(ctx, qryParams.BlackboardService.SequenceCounter);
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
    protected override async ValueTask OnExceptionAsync(IQueryActorContext context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(query);
        IsArgumentNull.Check(verb);
        IsArgumentNull.Check(ex?.Message!);

        try
        {
            var serviceResultTask = default(ValueTask) switch
            {
                _ when query is GetFuturesOptionContractQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesOptionContractReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetFuturesOptionSpreadDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesOptionSpreadDataReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetFuturesRiskPositionTypeQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<RiskPositionTypeReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetIronCondorMarketDataFeedQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<IronCondorMarketDataFeedReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetNormalCurveTableQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<NormalCurveTableReadModel>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionQuoteIdQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarValue<int>>(query.ErrorCode, ex!.Message)),
                _ when query is GetStreamingRequestIdQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<ScalarValue<int>>(query.ErrorCode, ex!.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex!.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}
