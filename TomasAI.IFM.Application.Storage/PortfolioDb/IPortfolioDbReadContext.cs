using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public interface IPortfolioDbReadContext
{
    // Financial book and committed operation receipts
    Task<T?> ReadOperationAsync<T>(int portfolioId,Guid operationId,string? inputHash=null,CancellationToken cancellationToken=default)
        where T:class,IFinancialCompletedEvent;
    Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId,CancellationToken cancellationToken=default);

    // Portfolio
    Task<PortfolioReadModel?> GetPortfolioAsync(int portfolioId,CancellationToken cancellationToken=default);
    Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int portfolioId,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state,int bucket,int afterPortfolioId,int pageSize,CancellationToken cancellationToken=default);

    // Fund
    Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId,int afterFundId,int pageSize,CancellationToken cancellationToken=default);
    Task<FundMandateReadModel?> GetFundAsync(int fundId,CancellationToken cancellationToken=default);
    Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int fundId,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId,int tradingYear,string decisionHorizon,DateTime effectiveAtUtc,int pageSize,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(int portfolioId,int fundId,long mandateVersion,string horizon,string root,DateTime asOfUtc,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId,int fundId,long mandateVersion,int pageSize,CancellationToken cancellationToken=default);
    Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId,int fundId,CancellationToken cancellationToken=default);
    Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId,int fundId,CancellationToken cancellationToken=default);

    // Order composition and trade order
    Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId,int fundId,DateOnly orderMonth,DateTime beforeUtc,int pageSize,CancellationToken cancellationToken=default);
    Task<FundOrderProjectionReadModel?> GetOrderAsync(int orderId,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int orderId,int pageSize,CancellationToken cancellationToken=default);
    Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int tradeId,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId,int pageSize,CancellationToken cancellationToken=default);

    // Financial policy
    Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int policyId,long? policyVersion=null,CancellationToken cancellationToken=default);
    Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int portfolioId,int pageSize,CancellationToken cancellationToken=default);
    Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int portfolioId,CancellationToken cancellationToken=default);
}
