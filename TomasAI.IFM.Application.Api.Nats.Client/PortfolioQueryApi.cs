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
        Send<GetPortfolioRequest, PortfolioReadModel>("GetPortfolio", portfolioId.ToString(), new(portfolioId, version), cancellationToken);

    public Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default) =>
        Send<GetPortfolioRevisionRequest, PortfolioAggregateRevision>("GetPortfolioRevision", portfolioId.ToString(), new(portfolioId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetPortfoliosRequest, PortfolioPage<PortfolioReadModel>>("GetPortfolios", "page", new(state is null ? null : (int)state.Value, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default) =>
        Send<GetFundRequest, FundMandateReadModel>("GetFund", $"{portfolioId}.{fundId}", new(portfolioId, fundId, version), cancellationToken);

    public Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) =>
        Send<GetFundRevisionRequest, PortfolioAggregateRevision>("GetFundRevision", $"{portfolioId}.{fundId}", new(portfolioId, fundId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetFundsRequest, PortfolioPage<FundMandateReadModel>>("GetFunds", portfolioId.ToString(), new(portfolioId, state is null ? null : (int)state.Value, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default) =>
        Send<GetAllocationRequest, FundAllocationReadModel>("GetFundAllocation", $"{portfolioId}.{fundId}", new(portfolioId, fundId), cancellationToken);

    public Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        Send<GetEnvelopeRequest, FundRiskEnvelopeReadModel>("GetFundRiskEnvelope", $"{portfolioId}.{fundId}", new(portfolioId, fundId, asOfUtc), cancellationToken);

    public Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default) =>
        Send<GetAssignmentsRequest, FundTradeTemplateAssignmentReadModel[]>("GetFundTemplateAssignments", $"{portfolioId}.{fundId}", new(portfolioId, fundId, mandateVersion), cancellationToken);

    public Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default) =>
        Send<GetStrategySnapshotRequest, PortfolioFundStrategySnapshot>("GetPortfolioFundStrategySnapshot", portfolioId.ToString(), new(portfolioId, tradingYear, decisionHorizon, underlyingRoot, assetType, asOfUtc, workflowId, workflowRevision, correlationId), cancellationToken);

    public Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default) =>
        Send<GetOrderRequest, FundOrderProjectionReadModel>("GetFundOrderByOrderId", orderId.ToString(), new(orderId), cancellationToken);

    public Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default) =>
        Send<GetTradeRequest, FundOrderTradeProjectionReadModel>("GetFundOrderTradeByTradeId", tradeId.ToString(), new(tradeId), cancellationToken);

    public Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default) =>
        Send<GetCompositionRequest, FundCompositionWorkflowProjectionReadModel[]>("GetFundCompositionByWorkflow", workflowId.ToString("N"), new(workflowId), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetOrdersRequest, PortfolioPage<FundOrderProjectionReadModel>>("GetFundOrdersPage", $"{portfolioId}.{fundId}", new(portfolioId, fundId, orderMonth, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default) =>
        Send<GetOrderTradesRequest, PortfolioPage<FundOrderTradeProjectionReadModel>>("GetFundOrderTradesPage", orderId.ToString(), new(orderId, pageSize, pageToken), cancellationToken);

    public Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default) =>
        Send<GetStrategyReferenceCombinationsRequest, PortfolioFundStrategyReferenceCombination[]>("GetPortfolioFundStrategyReferenceCombinations", portfolioId.ToString(), new(portfolioId, asOfUtc), cancellationToken);

    public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) =>
        Send<GetPolicyRequest, PortfolioFinancialPolicyReadModel>("GetPortfolioFinancialPolicy", policyId.ToString(), new(policyId, policyVersion), cancellationToken);
    public Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default) =>
        Send<GetPoliciesRequest, PortfolioPage<PortfolioFinancialPolicyReadModel>>("GetPortfolioFinancialPolicies", portfolioId.ToString(), new(portfolioId, pageSize), cancellationToken);
    public Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) =>
        Send<GetActivePolicyRequest, PortfolioFinancialPolicyReadModel>("GetActivePortfolioFinancialPolicy", portfolioId.ToString(), new(portfolioId), cancellationToken);

    async Task<ServiceResult<TResult>> Send<TParameters, TResult>(string verb, string entityKey, TParameters parameters, CancellationToken cancellationToken)
        where TResult : class
    {
        var subject = new ActorSubject(ActorType.Query, PortfolioQuerySubjects.Actor, verb, entityKey);
        var query = new PortfolioQuery<TParameters, TResult>
        {
            Subject = subject,
            Parameters = parameters,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(),
            RequestedOnUtc = DateTime.UtcNow,
            Access = PortfolioAccessScope.Current ?? PortfolioAccessContext.Reader($"interactive:{Environment.UserName}"),
        };
        return await RequestAsync<PortfolioQuery<TParameters, TResult>, TResult>(subject, query, cancellationToken).ConfigureAwait(false);
    }
}
