using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Extensions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Query.Actor;

/// <summary>
/// Represents an actor responsible for managing reference data queries within an event-sourced system.
/// </summary>
/// <remarks>The <see cref="ReferenceQueryActor"/> is a specialized query actor designed to handle operations
/// related to reference data such as seed IDs, futures contract definitions, and MDI forward loss ratios.
/// This actor relies on the reference database for data retrieval and uses dependency injection to resolve
/// required services.</remarks>
/// <param name="logger">Logger for recording actor operations and errors.</param>
public class ReferenceQueryActor(IQueryActorContext<ReferenceQueryActor> actorContext)
    : BaseQueryActor<ReferenceQueryActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "ReferenceQuery";
    readonly ILogger<ReferenceQueryActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    protected IReferenceQueryContext ReferenceQueryContext { get; } =
        IsArgumentNull.Set(actorContext as IReferenceQueryContext, nameof(actorContext))!;

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
    protected override IQuery ParseMessage(IQueryActorContext<ReferenceQueryActor> context, IActorMessage message)
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
        [GetLookupDefinitionsQuery.Verb] = msg => msg.AsQuery<GetLookupDefinitionsQuery, LookupDefinitionReadModel[]>()!,
        [TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery.Verb] = msg => msg.AsQuery<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery, string>()!,
        [GetCurrentSeedIdQuery.Verb] = msg => msg.AsQuery<GetCurrentSeedIdQuery, ScalarReadModel<int>>()!,
        [GetNextSeedIdQuery.Verb] = msg => msg.AsQuery<GetNextSeedIdQuery, ScalarReadModel<int>>()!,
        [GetDefaultFuturesContractDefinitionsQuery.Verb] = msg => msg.AsQuery<GetDefaultFuturesContractDefinitionsQuery, DefaultFuturesContractDefinitionsReadModel>()!,
        [GetFuturesOptionStrikePriceDefinitionsQuery.Verb] = msg => msg.AsQuery<GetFuturesOptionStrikePriceDefinitionsQuery, FuturesOptionStrikePriceReadModel>()!,
        [GetMDIForwardLossRatiosQuery.Verb] = msg => msg.AsQuery<GetMDIForwardLossRatiosQuery, MDIForwardLossRatioReadModel[]>()!
        ,[GetTradeStrategyFamiliesQuery.Verb] = msg => msg.AsQuery<GetTradeStrategyFamiliesQuery, TradeStrategyFamilyReadModel[]>()!
        ,[GetTradeStrategySymbolsQuery.Verb] = msg => msg.AsQuery<GetTradeStrategySymbolsQuery, TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <remarks>This method processes queries using a dictionary-based dispatch pattern that maps query type names
    /// to their corresponding handler functions. Each handler executes the query against the reference query state and
    /// returns the appropriate result.</remarks>
    /// <param name="context">The context in which the query is being processed, providing access to actor-specific information.</param>
    /// <param name="query">The query to be processed. Cannot be null.</param>
    /// <returns>A ValueTask that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the incoming query type is not supported by the actor.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<ReferenceQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<ReferenceQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(ReferenceQueryContext, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding reference query
    /// logic against the query state.
    /// </summary>
    /// <remarks>This dictionary enables dynamic dispatch of reference-related queries by associating each query
    /// type name with a function that processes the query against a ReferenceQueryState. The mapping is intended for
    /// internal use to streamline query handling and should not be modified at runtime.</remarks>
    static readonly IReadOnlyDictionary<Type, Func<IReferenceQueryContext, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IReferenceQueryContext, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetLookupDefinitionsQuery)] = async (ctx, q, ct) =>
        {
            var rows = await ctx.DbFactory.ConfigurationDb.GetLookupDefinitionsAsync(((GetLookupDefinitionsQuery)q).GroupName, ct);
            ct.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetLookupDefinitionsQuery.Verb, new ServiceOk<LookupDefinitionReadModel[]>(rows));
        },
        [typeof(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery)] = async (ctx, q, ct) =>
        {
            var query = (TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery)q;
            var request = TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogJson.Read<TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogQueryRequest>(query.RequestJson);
            var value = await new TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService(ctx.DbFactory).QueryAsync(request, ct);
            await ctx.ReplyAsync(q.Subject.ThreadId, TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogQuery.Verb, new ServiceOk<string>(value));
        },
        [typeof(GetTradeStrategySymbolsQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = (GetTradeStrategySymbolsQuery)q;
            var result = ctx.MarketDataApi is null
                ? new ServiceFailed<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]>(GetTradeStrategySymbolsQuery.ErrorId, "Market-data API is unavailable.")
                : await ctx.MarketDataApi.GetTradeStrategySymbolsAsync(query.Family, cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeStrategySymbolsQuery.Verb, result);
        },
        [typeof(GetCurrentSeedIdQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = IsArgumentNull.Set(q as GetCurrentSeedIdQuery);
            var result = await query.GetCurrentSeedIdAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetCurrentSeedIdQuery.Verb,
                new ServiceResult<ScalarReadModel<int>>(result));
        },
        [typeof(GetNextSeedIdQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = IsArgumentNull.Set(q as GetNextSeedIdQuery);
            var result = await query.GetNextSeedIdAsync(ctx.DbFactory, cancellationToken);
            await ctx.ReplyAsync(q.Subject.ThreadId, GetNextSeedIdQuery.Verb,
                new ServiceResult<ScalarReadModel<int>>(result));
        },
        [typeof(GetDefaultFuturesContractDefinitionsQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = IsArgumentNull.Set(q as GetDefaultFuturesContractDefinitionsQuery);
            var result = await query.GetDefaultFuturesContractDefinitionsAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetDefaultFuturesContractDefinitionsQuery.Verb,
                new ServiceResult<DefaultFuturesContractDefinitionsReadModel>(result));
        },
        [typeof(GetFuturesOptionStrikePriceDefinitionsQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = IsArgumentNull.Set(q as GetFuturesOptionStrikePriceDefinitionsQuery);
            var result = await query.GetFuturesOptionStrikePriceDefinitionsAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionStrikePriceDefinitionsQuery.Verb,
                new ServiceResult<FuturesOptionStrikePriceReadModel>(result));
        },
        [typeof(GetMDIForwardLossRatiosQuery)] = async (ctx, q, cancellationToken) =>
        {
            var query = IsArgumentNull.Set(q as GetMDIForwardLossRatiosQuery);
            var result = await query.GetMDIForwardLossRatiosAsync(ctx.DbFactory, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetMDIForwardLossRatiosQuery.Verb,
                new ServiceResult<MDIForwardLossRatioReadModel[]>(result));
        },
        [typeof(GetTradeStrategyFamiliesQuery)] = async (ctx, q, cancellationToken) =>
        {
            var result = await ctx.GetTradeStrategyFamiliesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetTradeStrategyFamiliesQuery.Verb, result);
        },
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
        IQueryActorContext<ReferenceQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}
