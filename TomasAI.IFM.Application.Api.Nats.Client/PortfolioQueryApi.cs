using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Production typed NATS client for every public Portfolio read operation.</summary>
public sealed class PortfolioQueryApi(IActorProducer actorProducer) : NatsClientApi(actorProducer), IPortfolioQueryApi
{
    public Task<ServiceResult<PortfolioReadModel>> GetPortfolioAsync(int portfolioId, long? version = null, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioQuery, PortfolioReadModel>(GetPortfolioQuery.Verb, portfolioId.ToString(), new(portfolioId, version), cancellationToken);

    public Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioRevisionQuery, PortfolioAggregateRevision>(GetPortfolioRevisionQuery.Verb, portfolioId.ToString(), new(portfolioId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetPortfoliosQuery, PortfolioPage<PortfolioReadModel>>(GetPortfoliosQuery.Verb, "page", new(state is null ? null : (int)state.Value, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default) =>
        Send<GetFundQuery, FundMandateReadModel>(GetFundQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId, version), cancellationToken);

    public Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) =>
        Send<GetFundRevisionQuery, PortfolioAggregateRevision>(GetFundRevisionQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetFundsQuery, PortfolioPage<FundMandateReadModel>>(GetFundsQuery.Verb, portfolioId.ToString(), new(portfolioId, state is null ? null : (int)state.Value, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) =>
        Send<GetFundAllocationQuery, FundAllocationReadModel>(GetFundAllocationQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId), cancellationToken);

    public Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        Send<GetFundRiskEnvelopeQuery, FundRiskEnvelopeReadModel>(GetFundRiskEnvelopeQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId, asOfUtc), cancellationToken);

    public Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default) =>
        Send<GetFundTemplateAssignmentsQuery, FundTradeTemplateAssignmentReadModel[]>(GetFundTemplateAssignmentsQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId, mandateVersion), cancellationToken);

    public Task<ServiceResult<PortfolioFundStrategySnapshot>> ResolveForSelectionAsync(int portfolioId, int? fundId, int tradingYear, string decisionHorizon, string underlyingRoot, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default) =>
        Send<ResolveForSelectionQuery, PortfolioFundStrategySnapshot>(ResolveForSelectionQuery.Verb, portfolioId.ToString(), new(portfolioId, fundId, tradingYear, decisionHorizon, underlyingRoot, asOfUtc, workflowId, workflowRevision, correlationId), cancellationToken);

    public Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioFundStrategySnapshotQuery, PortfolioFundStrategySnapshot>(GetPortfolioFundStrategySnapshotQuery.Verb, portfolioId.ToString(), new(portfolioId, tradingYear, decisionHorizon, underlyingRoot, assetType, asOfUtc, workflowId, workflowRevision, correlationId), cancellationToken);

    public Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        Send<GetFundOrderByOrderIdQuery, FundOrderProjectionReadModel>(GetFundOrderByOrderIdQuery.Verb, orderId.ToString(), new(orderId), cancellationToken);

    public Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default) =>
        Send<GetFundOrderTradeByTradeIdQuery, FundOrderTradeProjectionReadModel>(GetFundOrderTradeByTradeIdQuery.Verb, tradeId.ToString(), new(tradeId), cancellationToken);

    public Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default) =>
        Send<GetFundCompositionByWorkflowQuery, FundCompositionWorkflowProjectionReadModel[]>(GetFundCompositionByWorkflowQuery.Verb, workflowId.ToString("N"), new(workflowId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetFundOrdersPageQuery, PortfolioPage<FundOrderProjectionReadModel>>(GetFundOrdersPageQuery.Verb, $"{portfolioId}.{fundId}", new(portfolioId, fundId, orderMonth, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetFundOrderTradesPageQuery, PortfolioPage<FundOrderTradeProjectionReadModel>>(GetFundOrderTradesPageQuery.Verb, orderId.ToString(), new(orderId, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioFundStrategyReferenceCombinationsQuery, PortfolioFundStrategyReferenceCombination[]>(GetPortfolioFundStrategyReferenceCombinationsQuery.Verb, portfolioId.ToString(), new(portfolioId, asOfUtc), cancellationToken);

    public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>(GetPortfolioFinancialPolicyQuery.Verb, policyId.ToString(), new(policyId, policyVersion), cancellationToken);
    public Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioFinancialPoliciesQuery, PortfolioPage<PortfolioFinancialPolicyReadModel>>(GetPortfolioFinancialPoliciesQuery.Verb, portfolioId.ToString(), new(portfolioId, pageSize), cancellationToken);
    public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) =>
        Send<GetActivePortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>(GetActivePortfolioFinancialPolicyQuery.Verb, portfolioId.ToString(), new(portfolioId), cancellationToken);

    async Task<ServiceResult<TResult>> Send<TQuery, TResult>(string verb, string entityKey, TQuery queryMessage, CancellationToken cancellationToken)
        where TResult : class
    {
        var actor = queryMessage switch
        {
            GetFundQuery => PortfolioQueryRoutes.Fund,
            GetFundRevisionQuery => PortfolioQueryRoutes.Fund,
            GetFundsQuery => PortfolioQueryRoutes.Fund,
            GetFundAllocationQuery => PortfolioQueryRoutes.Fund,
            GetFundRiskEnvelopeQuery => PortfolioQueryRoutes.Fund,
            GetFundTemplateAssignmentsQuery => PortfolioQueryRoutes.Fund,
            ResolveForSelectionQuery => PortfolioQueryRoutes.Fund,
            GetPortfolioFundStrategySnapshotQuery => PortfolioQueryRoutes.Fund,
            GetFundOrderByOrderIdQuery => PortfolioQueryRoutes.Fund,
            GetFundOrderTradeByTradeIdQuery => PortfolioQueryRoutes.Fund,
            GetFundCompositionByWorkflowQuery => PortfolioQueryRoutes.Fund,
            GetFundOrdersPageQuery => PortfolioQueryRoutes.Fund,
            GetFundOrderTradesPageQuery => PortfolioQueryRoutes.Fund,
            GetPortfolioFundStrategyReferenceCombinationsQuery => PortfolioQueryRoutes.Fund,
            GetPortfolioFinancialPolicyQuery => PortfolioQueryRoutes.FinancialPolicy,
            GetPortfolioFinancialPoliciesQuery => PortfolioQueryRoutes.FinancialPolicy,
            GetActivePortfolioFinancialPolicyQuery => PortfolioQueryRoutes.FinancialPolicy,
            _ => PortfolioQueryRoutes.Portfolio,
        };
        var subject = new ActorSubject(ActorType.Query, actor, verb, entityKey);
        var entityId = new ActorEntityId(entityKey);
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedOnUtc = DateTime.UtcNow;
        var access = PortfolioAccessScope.Current ?? PortfolioAccessContext.Reader($"interactive:{Environment.UserName}");
        object query = queryMessage switch
        {
            GetPortfolioQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfolioRevisionQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfoliosQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundRevisionQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundsQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundAllocationQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundRiskEnvelopeQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundTemplateAssignmentsQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            ResolveForSelectionQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfolioFundStrategySnapshotQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundOrderByOrderIdQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundOrderTradeByTradeIdQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundCompositionByWorkflowQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundOrdersPageQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetFundOrderTradesPageQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfolioFundStrategyReferenceCombinationsQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfolioFinancialPolicyQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetPortfolioFinancialPoliciesQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            GetActivePortfolioFinancialPolicyQuery value => value with { Subject = subject, EntityId = entityId, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = access },
            _ => throw new InvalidOperationException($"Unsupported Portfolio query message {typeof(TQuery).FullName}."),
        };
        var result = query switch
        {
            GetPortfolioQuery value => (object)await RequestAsync<GetPortfolioQuery, PortfolioReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfolioRevisionQuery value => (object)await RequestAsync<GetPortfolioRevisionQuery, PortfolioAggregateRevision>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfoliosQuery value => (object)await RequestAsync<GetPortfoliosQuery, PortfolioPage<PortfolioReadModel>>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundQuery value => (object)await RequestAsync<GetFundQuery, FundMandateReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundRevisionQuery value => (object)await RequestAsync<GetFundRevisionQuery, PortfolioAggregateRevision>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundsQuery value => (object)await RequestAsync<GetFundsQuery, PortfolioPage<FundMandateReadModel>>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundAllocationQuery value => (object)await RequestAsync<GetFundAllocationQuery, FundAllocationReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundRiskEnvelopeQuery value => (object)await RequestAsync<GetFundRiskEnvelopeQuery, FundRiskEnvelopeReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundTemplateAssignmentsQuery value => (object)await RequestAsync<GetFundTemplateAssignmentsQuery, FundTradeTemplateAssignmentReadModel[]>(subject, value, cancellationToken).ConfigureAwait(false),
            ResolveForSelectionQuery value => (object)await RequestAsync<ResolveForSelectionQuery, PortfolioFundStrategySnapshot>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfolioFundStrategySnapshotQuery value => (object)await RequestAsync<GetPortfolioFundStrategySnapshotQuery, PortfolioFundStrategySnapshot>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundOrderByOrderIdQuery value => (object)await RequestAsync<GetFundOrderByOrderIdQuery, FundOrderProjectionReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundOrderTradeByTradeIdQuery value => (object)await RequestAsync<GetFundOrderTradeByTradeIdQuery, FundOrderTradeProjectionReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundCompositionByWorkflowQuery value => (object)await RequestAsync<GetFundCompositionByWorkflowQuery, FundCompositionWorkflowProjectionReadModel[]>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundOrdersPageQuery value => (object)await RequestAsync<GetFundOrdersPageQuery, PortfolioPage<FundOrderProjectionReadModel>>(subject, value, cancellationToken).ConfigureAwait(false),
            GetFundOrderTradesPageQuery value => (object)await RequestAsync<GetFundOrderTradesPageQuery, PortfolioPage<FundOrderTradeProjectionReadModel>>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfolioFundStrategyReferenceCombinationsQuery value => (object)await RequestAsync<GetPortfolioFundStrategyReferenceCombinationsQuery, PortfolioFundStrategyReferenceCombination[]>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfolioFinancialPolicyQuery value => (object)await RequestAsync<GetPortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            GetPortfolioFinancialPoliciesQuery value => (object)await RequestAsync<GetPortfolioFinancialPoliciesQuery, PortfolioPage<PortfolioFinancialPolicyReadModel>>(subject, value, cancellationToken).ConfigureAwait(false),
            GetActivePortfolioFinancialPolicyQuery value => (object)await RequestAsync<GetActivePortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>(subject, value, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported Portfolio query {query.GetType().FullName}."),
        };
        return (ServiceResult<TResult>)result;
    }
}
