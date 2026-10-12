using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

public interface IPortfolioQueryApi
{
    Task<ServiceResult<PortfolioFundStrategySnapshot>> ResolveForSelectionAsync(int portfolioId, int? fundId, int tradingYear, string decisionHorizon, string underlyingRoot, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default) => throw new NotSupportedException("Selection resolution is unavailable.");
    Task<ServiceResult<PortfolioReadModel>> GetPortfolioAsync(int portfolioId, long? version = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioAggregateRevision>> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<PortfolioReadModel>>> GetPortfoliosAsync(PortfolioOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundMandateReadModel>> GetFundAsync(int portfolioId, int fundId, long? version = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioAggregateRevision>> GetFundRevisionAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundMandateReadModel>>> GetFundsAsync(int portfolioId, FundOperatingState? state, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundAllocationReadModel>> GetFundAllocationAsync(int portfolioId, int fundId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundRiskEnvelopeReadModel>> GetFundRiskEnvelopeAsync(int portfolioId, int fundId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundTradeTemplateAssignmentReadModel[]>> GetAssignmentsAsync(int portfolioId, int fundId, long mandateVersion, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFundStrategySnapshot>> GetStrategySnapshotAsync(int portfolioId, int tradingYear, string decisionHorizon, string underlyingRoot, string assetType, DateTime asOfUtc, Guid workflowId, long workflowRevision, Guid correlationId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderProjectionReadModel>> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundOrderTradeProjectionReadModel>> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default);
    Task<ServiceResult<FundCompositionWorkflowProjectionReadModel[]>> GetCompositionByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>> GetOrdersAsync(int portfolioId, int fundId, DateOnly orderMonth, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioPage<FundOrderTradeProjectionReadModel>>> GetOrderTradesAsync(int orderId, int pageSize, string? pageToken = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFundStrategyReferenceCombination[]>> GetStrategyReferenceCombinationsAsync(int portfolioId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetPolicyAsync(int policyId, long? policyVersion = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<PortfolioPage<PortfolioFinancialPolicyReadModel>>> GetPoliciesAsync(int portfolioId, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    Task<ServiceResult<PortfolioFinancialPolicyReadModel>> GetActivePolicyAsync(int portfolioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
