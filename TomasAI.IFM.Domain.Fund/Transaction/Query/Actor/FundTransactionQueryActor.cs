using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Queries;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing futures contract commands and state within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="FundTransactionQueryActor"/> is a specialized command actor designed to handle operations
/// related to futures contracts. It processes commands such as adding, changing, and removing futures contracts,
/// validates the commands, and manages the actor's state. This actor relies on an event-sourced repository for state
/// persistence and uses dependency injection to resolve required services.</remarks>
/// <param name="actorContext">The typed Fund transaction query context.</param>
public class FundTransactionQueryActor(IQueryActorContext<FundTransactionQueryActor> actorContext)
    : BaseQueryActor<FundTransactionQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "FundTransactionQuery";

    readonly ILogger<FundTransactionQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    /// <summary>Gets the Fund transaction query context supplied to this actor.</summary>
    protected IFundTransactionQueryContext FundTransactionQueryContext { get; } =
        IsArgumentNull.Set(actorContext as IFundTransactionQueryContext, nameof(actorContext))!;

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
    protected override IQuery ParseMessage(IQueryActorContext<FundTransactionQueryActor> context, IActorMessage message)
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
        [GetFundTransactionsQuery.Verb] = msg => msg.AsQuery<GetFundTransactionsQuery, FundTransactionReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the fund transaction query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context for the current actor query operation. Provides access to the incoming message and related metadata.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<FundTransactionQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<FundTransactionQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(query, FundTransactionQueryContext, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding fund transaction query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of fund transaction-related queries by associating each query
    /// type name with a function that processes the query against a FundTransactionQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IFundTransactionQueryContext, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQuery, IFundTransactionQueryContext, CancellationToken, ValueTask>>()
    {
        [typeof(GetFundTransactionsQuery)] = async (q, ctx, cancellationToken) =>
        {
            var query = (q as GetFundTransactionsQuery)!;
            var result = await ctx.GetFundTransactionsAsync(query, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFundTransactionsQuery.Verb,
                new ServiceResult<FundTransactionReadModel[]>(result));
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
    /// <param name="verb">The verb representing the type of query being processed.</param>
    /// <param name="ex">The exception that was thrown during query processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys);

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<FundTransactionQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
