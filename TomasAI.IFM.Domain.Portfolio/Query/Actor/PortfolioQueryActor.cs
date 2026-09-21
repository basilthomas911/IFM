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
using AllocatePortfolioBusinessIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.AllocatePortfolioBusinessIdRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioBusinessIdAllocation>;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePolicyRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetAllocationRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundAllocationReadModel>;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetCompositionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundCompositionWorkflowProjectionReadModel[]>;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrderRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel>;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetTradeRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel>;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrderTradesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel>>;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrdersRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel>>;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel>;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioAggregateRevision>;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetEnvelopeRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundRiskEnvelopeReadModel>;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel>>;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetAssignmentsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundTradeTemplateAssignmentReadModel[]>;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPoliciesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>>;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPolicyRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategyReferenceCombinationsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioFundStrategyReferenceCombination[]>;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionRequest, TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFundStrategySnapshot>;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategySnapshotRequest, TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFundStrategySnapshot>;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel>;
using GetPortfolioRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRevisionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioAggregateRevision>;
using GetPortfoliosQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfoliosRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel>>;

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
    public const string ActorName = PortfolioQuerySubjects.Actor;
    readonly PortfolioQueryParameters _parameters = new(RequireContext(actorContext));

    static IPortfolioQueryContext RequireContext(IQueryActorContext<PortfolioQueryActor> context) =>
        context as IPortfolioQueryContext
        ?? throw new ArgumentException("PortfolioQueryActor requires its Portfolio query context.", nameof(context));

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [PortfolioQueryVerbs.GetPortfolio] = static message => message.AsQuery<GetPortfolioQuery, PortfolioReadModel>()!,
        [PortfolioQueryVerbs.GetPortfolioRevision] = static message => message.AsQuery<GetPortfolioRevisionQuery, PortfolioAggregateRevision>()!,
        [PortfolioQueryVerbs.GetPortfolios] = static message => message.AsQuery<GetPortfoliosQuery, PortfolioPage<PortfolioReadModel>>()!,
        [PortfolioQueryVerbs.GetFund] = static message => message.AsQuery<GetFundQuery, FundMandateReadModel>()!,
        [PortfolioQueryVerbs.GetFundRevision] = static message => message.AsQuery<GetFundRevisionQuery, PortfolioAggregateRevision>()!,
        [PortfolioQueryVerbs.GetFunds] = static message => message.AsQuery<GetFundsQuery, PortfolioPage<FundMandateReadModel>>()!,
        [PortfolioQueryVerbs.GetFundAllocation] = static message => message.AsQuery<GetFundAllocationQuery, FundAllocationReadModel>()!,
        [PortfolioQueryVerbs.GetFundRiskEnvelope] = static message => message.AsQuery<GetFundRiskEnvelopeQuery, FundRiskEnvelopeReadModel>()!,
        [PortfolioQueryVerbs.GetFundTemplateAssignments] = static message => message.AsQuery<GetFundTemplateAssignmentsQuery, FundTradeTemplateAssignmentReadModel[]>()!,
        [PortfolioQueryVerbs.ResolveForSelection] = static message => message.AsQuery<ResolveForSelectionQuery, PortfolioFundStrategySnapshot>()!,
        [PortfolioQueryVerbs.GetPortfolioFundStrategySnapshot] = static message => message.AsQuery<GetPortfolioFundStrategySnapshotQuery, PortfolioFundStrategySnapshot>()!,
        [PortfolioQueryVerbs.GetFundOrderByOrderId] = static message => message.AsQuery<GetFundOrderByOrderIdQuery, FundOrderProjectionReadModel>()!,
        [PortfolioQueryVerbs.GetFundOrderTradeByTradeId] = static message => message.AsQuery<GetFundOrderTradeByTradeIdQuery, FundOrderTradeProjectionReadModel>()!,
        [PortfolioQueryVerbs.GetFundCompositionByWorkflow] = static message => message.AsQuery<GetFundCompositionByWorkflowQuery, FundCompositionWorkflowProjectionReadModel[]>()!,
        [PortfolioQueryVerbs.GetFundOrdersPage] = static message => message.AsQuery<GetFundOrdersPageQuery, PortfolioPage<FundOrderProjectionReadModel>>()!,
        [PortfolioQueryVerbs.GetFundOrderTradesPage] = static message => message.AsQuery<GetFundOrderTradesPageQuery, PortfolioPage<FundOrderTradeProjectionReadModel>>()!,
        [PortfolioQueryVerbs.GetPortfolioFundStrategyReferenceCombinations] = static message => message.AsQuery<GetPortfolioFundStrategyReferenceCombinationsQuery, PortfolioFundStrategyReferenceCombination[]>()!,
        [PortfolioQueryVerbs.AllocatePortfolioBusinessId] = static message => message.AsQuery<AllocatePortfolioBusinessIdQuery, PortfolioBusinessIdAllocation>()!,
        [PortfolioQueryVerbs.GetPortfolioFinancialPolicy] = static message => message.AsQuery<GetPortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
        [PortfolioQueryVerbs.GetPortfolioFinancialPolicies] = static message => message.AsQuery<GetPortfolioFinancialPoliciesQuery, PortfolioPage<PortfolioFinancialPolicyReadModel>>()!,
        [PortfolioQueryVerbs.GetActivePortfolioFinancialPolicy] = static message => message.AsQuery<GetActivePortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
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
            [typeof(GetFundQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundRevisionQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundRevisionQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundsQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundsQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundAllocationQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundAllocationQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundRiskEnvelopeQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundRiskEnvelopeQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundTemplateAssignmentsQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundTemplateAssignmentsQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(ResolveForSelectionQuery)] = static (parameters, context, query, cancellationToken) =>
                ((ResolveForSelectionQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfolioFundStrategySnapshotQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioFundStrategySnapshotQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundOrderByOrderIdQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundOrderByOrderIdQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundOrderTradeByTradeIdQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundOrderTradeByTradeIdQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundCompositionByWorkflowQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundCompositionByWorkflowQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundOrdersPageQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundOrdersPageQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetFundOrderTradesPageQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetFundOrderTradesPageQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfolioFundStrategyReferenceCombinationsQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioFundStrategyReferenceCombinationsQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(AllocatePortfolioBusinessIdQuery)] = static (parameters, context, query, cancellationToken) =>
                ((AllocatePortfolioBusinessIdQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfolioFinancialPolicyQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioFinancialPolicyQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetPortfolioFinancialPoliciesQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetPortfolioFinancialPoliciesQuery)query).ExecuteAsync(context, parameters, cancellationToken),
            [typeof(GetActivePortfolioFinancialPolicyQuery)] = static (parameters, context, query, cancellationToken) =>
                ((GetActivePortfolioFinancialPolicyQuery)query).ExecuteAsync(context, parameters, cancellationToken)
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
        var request = (IPortfolioRequestMetadata)query;
        using var activity = PortfolioTelemetry.StartRequest("query", query.Subject.Verb, request);
        var allocatesIdentity = query is AllocatePortfolioBusinessIdQuery;
        operationalGuard.Demand(allocatesIdentity ? PortfolioOperation.AdministerPortfolio : PortfolioOperation.Read,
            request, mutation: allocatesIdentity);
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
