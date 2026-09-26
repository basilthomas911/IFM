using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using AllocatePortfolioBusinessIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.AllocatePortfolioBusinessIdQuery;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePortfolioFinancialPolicyQuery;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundCompositionByWorkflowQuery;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderByOrderIdQuery;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradeByTradeIdQuery;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradesPageQuery;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrdersPageQuery;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundQuery;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionQuery;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRiskEnvelopeQuery;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundTemplateAssignmentsQuery;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPoliciesQuery;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFinancialPolicyQuery;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategyReferenceCombinationsQuery;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionQuery;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioQuery;
using GetPortfolioRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRevisionQuery;
using GetPortfoliosQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfoliosQuery;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

public interface IPortfolioQueryContext : IQueryActorContext<PortfolioQueryActor>
{
    IDbContextFactory DbFactory { get; }
    IPortfolioBusinessIdAllocator IdentityAllocator { get; }
    ILogger<PortfolioQueryActor> Logger { get; }
}

/// <summary>NATS-only public query boundary over PortfolioDb projections.</summary>
public sealed class PortfolioQueryActor(IQueryActorContext<PortfolioQueryActor> actorContext, IPortfolioOperationalGuard operationalGuard)
    : BaseQueryActor<PortfolioQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    public const string ActorName = GetPortfolioQuery.Actor;
    readonly PortfolioQueryParameters _parameters = new(RequireContext(actorContext));

    static IPortfolioQueryContext RequireContext(IQueryActorContext<PortfolioQueryActor> context) =>
        context as IPortfolioQueryContext
        ?? throw new ArgumentException("PortfolioQueryActor requires its Portfolio query context.", nameof(context));

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetPortfolioQuery.Verb] = static message => message.AsQuery<GetPortfolioQuery, PortfolioReadModel>()!,
        [GetPortfolioRevisionQuery.Verb] = static message => message.AsQuery<GetPortfolioRevisionQuery, PortfolioAggregateRevision>()!,
        [GetPortfoliosQuery.Verb] = static message => message.AsQuery<GetPortfoliosQuery, PortfolioPage<PortfolioReadModel>>()!,
        [AllocatePortfolioBusinessIdQuery.Verb] = static message => message.AsQuery<AllocatePortfolioBusinessIdQuery, PortfolioBusinessIdAllocation>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetPortfolioQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfolioRevisionQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioRevisionQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfoliosQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfoliosQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(AllocatePortfolioBusinessIdQuery)] = static (parameters, context, query, cancellationToken) =>
                ((AllocatePortfolioBusinessIdQuery)query).ExecuteAsync(context, parameters, cancellationToken),
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) => exception switch
        {
            PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
            PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
            _ when query is AllocatePortfolioBusinessIdQuery => PortfolioErrorCodes.SequenceAllocationFailed,
            _ => PortfolioErrorCodes.ValidationFailed,
        });

    protected override ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        dynamic request = query;
        using var activity = PortfolioTelemetry.StartRequest("query", query.Subject.Verb, request.CorrelationId);
        var allocatesIdentity = query is AllocatePortfolioBusinessIdQuery;
        operationalGuard.Demand(allocatesIdentity ? PortfolioOperation.AdministerPortfolio : PortfolioOperation.Read,
            request.Access, mutation: allocatesIdentity);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var receive = ResolveMappedQueryHandler(query, _receiveMap);
            await receive(_parameters, context, query, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PortfolioTelemetry.QueryDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("portfolio.operation", query.Subject.Verb),
                new KeyValuePair<string, object?>("portfolio.outcome", "completed"));
        }
    }

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<PortfolioQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

}
