using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures option contract queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FuturesOptionContractQueryActor"/> is a specialized query actor designed to handle operations
/// related to futures option contracts. It processes queries such as retrieving specific contracts by ID,
/// contracts by symbol, and checking for existing contract IDs. This actor uses dependency injection to resolve required services.</remarks>
/// <param name="logger">The logger instance for tracking actor operations.</param>
public class FuturesOptionContractQueryActor(IQueryActorContext<FuturesOptionContractQueryActor> actorContext)
    : BaseQueryActor<FuturesOptionContractQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FuturesOptionContractQuery";
    readonly ILogger<FuturesOptionContractQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    protected IFuturesOptionContractQueryContext FuturesOptionContractContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesOptionContractQueryContext, nameof(actorContext))!;

    /// <summary>
    /// Parses the specified actor message and extracts the query associated with the message.
    /// </summary>
    /// <remarks>This method validates the provided message and resolves it to a specific query based on the
    /// message subject. The resolved query is then stored in the provided context along with additional message
    /// information.</remarks>
    /// <param name="context">The query actor context used to store message-related information. This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <param name="message">The actor message to parse. This parameter cannot be <see langword="null"/>.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<FuturesOptionContractQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    /// <remarks>This dictionary enables efficient dispatching and parsing of incoming NATS messages based on
    /// their verb. Each entry associates a specific query verb with a function that converts a NATS message payload
    /// into a strongly typed query object implementing the IQuery interface. The mapping is intended for internal
    /// use in query deserialization and routing scenarios.</remarks>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetFuturesOptionContractQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>()!,
        [GetFuturesOptionContractsPageQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractsPageQuery, FuturesOptionContractPageReadModel>()!,
        [GetFuturesOptionContractsQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractsQuery, FuturesOptionContractReadModel[]>()!,
        [GetFuturesOptionContractIdsQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionContractIdsQuery, string[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the futures option contract query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<FuturesOptionContractQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FuturesOptionContractQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(FuturesOptionContractContext, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding futures option contract query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of futures option contract-related queries by associating each query
    /// type name with a function that processes the query against a FuturesOptionContractQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly IReadOnlyDictionary<Type, Func<IFuturesOptionContractQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IFuturesOptionContractQueryContext, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetFuturesOptionContractQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = (q as GetFuturesOptionContractQuery)!;
            var result = await query.GetFuturesOptionContractAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractQuery.Verb,
                new ServiceResult<FuturesOptionContractReadModel?>(result));
        },
        [typeof(GetFuturesOptionContractsPageQuery)] = async (ctx, q, cancellationToken) =>
        {
            var result = await ((GetFuturesOptionContractsPageQuery)q).GetFuturesOptionContractsPageAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractsPageQuery.Verb,
                new ServiceResult<FuturesOptionContractPageReadModel>(result));
        },
        [typeof(GetFuturesOptionContractsQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = (q as GetFuturesOptionContractsQuery)!;
            var result = await query.GetFuturesOptionContractsAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractsQuery.Verb,
                new ServiceResult<FuturesOptionContractReadModel[]>(result));
        },
        [typeof(GetFuturesOptionContractIdsQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = (q as GetFuturesOptionContractIdsQuery)!;
            var result = await query.GetFuturesOptionContractIdsAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractIdsQuery.Verb,
                new ServiceResult<string[]>(result));
        }
    };

    /// <summary>
    /// Handles exceptions that occur during the processing of a query in the actor context.
    /// </summary>
    /// <remarks>This method attempts to handle the exception by determining the type of query that caused it
    /// and sending an appropriate error response back to the caller. If the query type is not recognized, a generic
    /// error response is sent.</remarks>
    /// <param name="context">The context in which the query is being processed. Provides access to actor-specific operations and state.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception occurred.</param>
    /// <param name="query">The query that encountered the exception.</param>
    /// <param name="verb">The verb associated with the query that caused the exception.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FuturesOptionContractQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
