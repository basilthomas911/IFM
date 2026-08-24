using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures trade signal queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesTradeSignalQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures trade signal lookups such as retrieving the last trade signal, trade signal by contract,
/// and trade signal IDs. It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="logger">The logger used to record diagnostic and operational information.</param>
public class FuturesTradeSignalQueryActor(
    IQueryActorContext<FuturesTradeSignalQueryActor> actorContext)
    : BaseQueryActor<FuturesTradeSignalQueryActor>(actorContext.Logger, actorContext.ActorId)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesTradeSignalQueryContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesTradeSignalQueryContext, nameof(actorContext))!;

    public const string ActorName = "FuturesTradeSignalQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
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
    static readonly Dictionary<string, Func<IActorMessage, IQuery>> _parseMap = new()
    {
        [GetFuturesTradeSignalQuery.Verb] = msg => msg.AsQuery<GetFuturesTradeSignalQuery, FuturesTradeSignalV2ReadModel>()!,
        [GetMarketOutlookSnapshotQuery.Verb] = msg => msg.AsQuery<GetMarketOutlookSnapshotQuery, MarketOutlookSnapshotReadModel>()!,
        [GetLastFuturesTradeSignalQuery.Verb] = msg => msg.AsQuery<GetLastFuturesTradeSignalQuery, FuturesTradeSignalV2ReadModel>()!,
        [GetFuturesTradeSignalIdsQuery.Verb] = msg => msg.AsQuery<GetFuturesTradeSignalIdsQuery, FuturesTradeSignalId[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IQuery query)
        => await ReceiveAsync(context, query, CancellationToken.None).ConfigureAwait(false);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = actorContext.RouteTo(context);
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(dispatchContext, actorContext.DbFactory, query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures trade signal query
    /// logic against the query state.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext<FuturesTradeSignalQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new()
    {
        [typeof(GetMarketOutlookSnapshotQuery).Name] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (GetMarketOutlookSnapshotQuery)q;
            var result = await db.MarketDataDb.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMarketOutlookSnapshotQuery.Verb,
                new ServiceResult<MarketOutlookSnapshotReadModel?>(result)).ConfigureAwait(false);
        },
        [typeof(GetFuturesTradeSignalQuery).Name] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesTradeSignalQuery)!;
            var result = await query.GetFuturesTradeSignalAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesTradeSignalQuery.Verb,
                new ServiceResult<FuturesTradeSignalV2ReadModel?>(result)).ConfigureAwait(false);
        },
        [typeof(GetLastFuturesTradeSignalQuery).Name] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetLastFuturesTradeSignalQuery)!;
            var result = await query.GetLastFuturesTradeSignalAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTradeSignalQuery.Verb,
                new ServiceResult<FuturesTradeSignalV2ReadModel?>(result)).ConfigureAwait(false);
        },
        [typeof(GetFuturesTradeSignalIdsQuery).Name] = async (ctx, db, q, cancellationToken) =>
        {
            var query = (q as GetFuturesTradeSignalIdsQuery)!;
            var result = await query.GetFuturesTradeSignalIdsAsync(db, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesTradeSignalIdsQuery.Verb,
                new ServiceResult<FuturesTradeSignalId[]>(result)).ConfigureAwait(false);
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
                _ when query is GetFuturesTradeSignalQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesTradeSignalV2ReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetMarketOutlookSnapshotQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<MarketOutlookSnapshotReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetLastFuturesTradeSignalQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesTradeSignalV2ReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetFuturesTradeSignalIdsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<FuturesTradeSignalId[]>(query.ErrorCode, ex!.Message)),
                _ => context.ReplyAsync(threadId, verb, new ServiceFailed<ActorEntityId>(9999, ex!.Message))
            };
            await serviceResultTask;
        }
        catch (Exception innerEx)
        {
            actorContext.Logger.LogError(innerEx, "Error handling exception in {ActorName} for thread {ThreadId}: {ErrorMessage}", ActorName, threadId, innerEx.Message);
        }
    }
}
