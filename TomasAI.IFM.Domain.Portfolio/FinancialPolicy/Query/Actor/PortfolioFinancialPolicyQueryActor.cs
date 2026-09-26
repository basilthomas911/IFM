using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPoliciesQuery;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePortfolioFinancialPolicyQuery;

namespace TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Query.Actor;

/// <summary>Owns financial-policy projection queries on the PortfolioFinancialPolicyQuery route.</summary>
/// <param name="actorContext">The Financial Policy query mailbox context.</param>
/// <param name="dbFactory">The Portfolio projection database factory.</param>
/// <param name="identityAllocator">The durable business-identity allocator.</param>
/// <param name="operationalGuard">The Portfolio read policy guard.</param>
/// <param name="logger">The typed actor logger.</param>
public sealed class PortfolioFinancialPolicyQueryActor(
    IQueryActorContext<PortfolioFinancialPolicyQueryActor> actorContext,
    IDbContextFactory dbFactory,
    IPortfolioBusinessIdAllocator identityAllocator,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFinancialPolicyQueryActor> logger)
    : BaseQueryActor<PortfolioFinancialPolicyQueryActor>(actorContext, logger)
{
    public const string ActorName = PortfolioQueryRoutes.FinancialPolicy;
    readonly PortfolioQueryParameters _parameters = new(dbFactory, identityAllocator);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetPortfolioFinancialPolicyQuery.Verb] = static message => message.AsQuery<GetPortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
        [GetPortfolioFinancialPoliciesQuery.Verb] = static message => message.AsQuery<GetPortfolioFinancialPoliciesQuery, PortfolioPage<PortfolioFinancialPolicyReadModel>>()!,
        [GetActivePortfolioFinancialPolicyQuery.Verb] = static message => message.AsQuery<GetActivePortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioFinancialPolicyQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioFinancialPolicyQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetPortfolioFinancialPolicyQuery)] = static (parameters, context, query, token) =>
            ((GetPortfolioFinancialPolicyQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetPortfolioFinancialPoliciesQuery)] = static (parameters, context, query, token) =>
            ((GetPortfolioFinancialPoliciesQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetActivePortfolioFinancialPolicyQuery)] = static (parameters, context, query, token) =>
            ((GetActivePortfolioFinancialPolicyQuery)query).ExecuteAsync(context, parameters, token),
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) => exception switch
        {
            PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
            PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
            _ => PortfolioErrorCodes.ValidationFailed,
        });

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioFinancialPolicyQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<PortfolioFinancialPolicyQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<PortfolioFinancialPolicyQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        dynamic request = query;
        using var activity = PortfolioTelemetry.StartRequest("query", query.Subject.Verb, request.CorrelationId);
        operationalGuard.Demand(PortfolioOperation.Read, request.Access, mutation: false);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await ResolveMappedQueryHandler(query, _receiveMap)(_parameters, context, query, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PortfolioTelemetry.QueryDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("portfolio.operation", query.Subject.Verb),
                new KeyValuePair<string, object?>("portfolio.outcome", "completed"));
        }
    }

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<PortfolioFinancialPolicyQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

/// <summary>Provides the Financial Policy query mailbox for the split actor route.</summary>
/// <param name="supervisor">The actor supervisor.</param>
public sealed class PortfolioFinancialPolicyQueryContext(IActorSupervisor supervisor)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, PortfolioFinancialPolicyQueryActor.ActorName)),
        IQueryActorContext<PortfolioFinancialPolicyQueryActor>;
