using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;

/// <summary>Provides the SpreadDistributionQueryActor implementation.</summary>
public class SpreadDistributionQueryActor(
    IQueryActorContext<SpreadDistributionQueryActor> actorContext)
    : BaseQueryActor<SpreadDistributionQueryActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected ISpreadDistributionQueryContext ActorContext =>
        IsArgumentNull.Set(Context as ISpreadDistributionQueryContext, nameof(Context))!;

    public const string ActorName = "SpreadDistributionQuery";

    /// <summary>
    /// Parses the specified actor message and extracts the thread identifier associated with the message.
    /// </summary>
    /// <param name="context">The query actor context used to store message-related information.</param>
    /// <param name="message">The actor message to parse.</param>
    /// <returns>The parsed query instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject cannot be resolved to a valid query for the actor.</exception>
    protected override IQuery ParseMessage(IQueryActorContext<SpreadDistributionQueryActor> context, IActorMessage message)
        => ParseMappedQuery(context, message, _parseMap);

    /// <summary>
    /// Provides a mapping from query verb strings to delegate functions that parse a NATS message into the
    /// corresponding query instance.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap = new Dictionary<string, Func<IActorMessage, IQuery>>()
    {
        [GetSpreadDistributionQuery.Verb] = msg => msg.AsQuery<GetSpreadDistributionQuery, SpreadDistributionReadModel>()!
    };

    /// <summary>
    /// Handles incoming queries asynchronously and processes them based on their type.
    /// </summary>
    /// <param name="context">The context in which the query is being processed.</param>
    /// <param name="query">The query to process.</param>
    /// <returns>A task that represents the asynchronous query processing operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the query type is not supported.</exception>
    protected override ValueTask ReceiveAsync(IQueryActorContext<SpreadDistributionQueryActor> context, IQuery query)
        => ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(
        IQueryActorContext<SpreadDistributionQueryActor> context,
        IQuery query,
        CancellationToken cancellationToken)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(query);
        var receiveFunc = ResolveMappedQueryHandler(query, _receiveMap);
        await receiveFunc.Invoke(dispatchContext, ActorContext.DbFactory, query, cancellationToken);
    }

    /// <summary>
    /// Provides a mapping from query type names to delegate functions that execute the corresponding spread distribution query
    /// logic against the database context factory.
    /// </summary>
    static readonly IReadOnlyDictionary<Type, Func<IQueryActorContext<SpreadDistributionQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>> _receiveMap = new Dictionary<Type, Func<IQueryActorContext<SpreadDistributionQueryActor>, IDbContextFactory, IQuery, CancellationToken, ValueTask>>()
    {
        [typeof(GetSpreadDistributionQuery)] = async (ctx, dbFactory, q, cancellationToken) =>
        {
            var query = (q as GetSpreadDistributionQuery)!;
            var result = await query.GetSpreadDistributionAsync(
                dbFactory,
                query.TradeId,
                query.TradeType,
                query.TradeStatus,
                query.ValueDate,
                query.DaysToExpiry,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ctx.ReplyAsync(q.Subject.ThreadId, GetSpreadDistributionQuery.Verb,
                new ServiceResult<SpreadDistributionReadModel?>(result));
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
        IQueryActorContext<SpreadDistributionQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

}
