using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Actor.Option.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Query;

/// <summary>
/// Represents an actor responsible for managing option trade queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="OptionTradeQueryActor"/> is a specialized query actor designed to handle operations
/// related to option trade lookups such as retrieving option trades, spread data, spread bar data,
/// option leg contract IDs, and iron condor trade prices.
/// It processes queries, validates them, and manages the actor's state.</remarks>
/// <param name="dbFactory">The database context factory used to access option trade data.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class OptionTradeQueryActor(
    IDbContextFactory dbFactory,
    ILogger<OptionTradeQueryActor> logger)
    : BaseQueryActor<OptionTradeQueryActor>(logger, new ActorMailboxId(ActorType.Query, ActorName))
{
    public const string ActorName = "OptionTradeQuery";

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
        [GetOptionTradeQuery.Verb] = msg => msg.AsQuery<GetOptionTradeQuery, OptionTradeReadModel>()!,
        [GetOptionTradesQuery.Verb] = msg => msg.AsQuery<GetOptionTradesQuery, OptionTradeReadModel[]>()!,
        [GetOptionTradeSpreadDataQuery.Verb] = msg => msg.AsQuery<GetOptionTradeSpreadDataQuery, OptionTradeSpreadsDataModel>()!,
        [GetOptionTradeSpreadBarDataQuery.Verb] = msg => msg.AsQuery<GetOptionTradeSpreadBarDataQuery, OptionTradeSpreadBarsDataModel[]>()!,
        [GetOptionLegContractIdsQuery.Verb] = msg => msg.AsQuery<GetOptionLegContractIdsQuery, string[]>()!,
        [GetIronCondorTradePriceQuery.Verb] = msg => msg.AsQuery<GetIronCondorTradePriceQuery, TradePriceReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="state">The current state of the actor, which must be of type <see cref="OptionTradeQueryState"/>.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override async ValueTask ReceiveAsync(IQueryActorContext context, IActorState state, IQuery query)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(query);
        var qryName = query.GetType().Name;
        if (!_receiveMap.TryGetValue(qryName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to process {ActorName} query: {qryName}");
        await receiveFunc.Invoke(context, dbFactory, query);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding option trade query
    /// logic against the database context factory.
    /// </summary>
    static readonly Dictionary<string, Func<IQueryActorContext, IDbContextFactory, IQuery, ValueTask>> _receiveMap = new()
    {
        [typeof(GetOptionTradeQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetOptionTradeQuery)!;
            return query.GetOptionTradeAsync(ctx, dbFactory);
        },
        [typeof(GetOptionTradesQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetOptionTradesQuery)!;
            return query.GetOptionTradesAsync(ctx, dbFactory);
        },
        [typeof(GetOptionTradeSpreadDataQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetOptionTradeSpreadDataQuery)!;
            return query.GetOptionTradeSpreadDataAsync(ctx, dbFactory);
        },
        [typeof(GetOptionTradeSpreadBarDataQuery).Name] = (ctx,  dbFactory, q) =>
        {
            var query = (q as GetOptionTradeSpreadBarDataQuery)!;
            return query.GetOptionTradeSpreadBarDataAsync(ctx, dbFactory);
        },
        [typeof(GetOptionLegContractIdsQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetOptionLegContractIdsQuery)!;
            return query.GetOptionLegContractIdsAsync(ctx, dbFactory);
        },
        [typeof(GetIronCondorTradePriceQuery).Name] = (ctx, dbFactory, q) =>
        {
            var query = (q as GetIronCondorTradePriceQuery)!;
            return query.GetIronCondorTradePriceAsync(ctx, dbFactory);
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
                _ when query is GetOptionTradeQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeReadModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradesQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeReadModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradeSpreadDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeSpreadsDataModel?>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionTradeSpreadBarDataQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<OptionTradeSpreadBarsDataModel[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetOptionLegContractIdsQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<string[]>(query.ErrorCode, ex!.Message)),
                _ when query is GetIronCondorTradePriceQuery
                    => context.ReplyAsync(threadId, verb, new ServiceResult<TradePriceReadModel?>(query.ErrorCode, ex!.Message)),
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
