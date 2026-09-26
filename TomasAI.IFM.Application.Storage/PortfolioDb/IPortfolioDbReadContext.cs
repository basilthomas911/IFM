using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Defines Portfolio database queries.</summary>
public interface IPortfolioDbReadContext
{
    /// <summary>Reads a committed financial operation receipt.</summary>
    /// <typeparam name="T">The completed financial event type.</typeparam>
    /// <param name="portfolioId">The portfolio identifier.</param>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="inputHash">The optional expected input hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The committed receipt, or <see langword="null"/>.</returns>
    Task<T?> ReadOperationAsync<T>(
        int portfolioId,
        Guid operationId,
        string? inputHash = null,
        CancellationToken cancellationToken = default)
        where T : class, IFinancialCompletedEvent;

    /// <summary>Reads the financial book for a portfolio.</summary>
    /// <param name="portfolioId">The portfolio identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The financial book, or <see langword="null"/>.</returns>
    Task<FinancialBookConfiguration?> ReadBookAsync(
        int portfolioId,
        CancellationToken cancellationToken = default);

    /// <summary>Reads an active financial book by execution-account identity.</summary>
    /// <param name="environment">The execution environment.</param>
    /// <param name="executionAccountReference">The execution-account reference.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active financial book, or <see langword="null"/>.</returns>
    Task<FinancialBookConfiguration?> ReadActiveBookByExecutionAccountAsync(
        string environment,
        string executionAccountReference,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a portfolio projection.</summary>
    /// <param name="portfolioId">The portfolio identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The projection, or <see langword="null"/> when absent.</returns>
    Task<PortfolioReadModel?> GetPortfolioAsync(int portfolioId, CancellationToken cancellationToken = default);

    /// <summary>Gets a portfolio projection revision.</summary>
    /// <param name="portfolioId">The portfolio identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The revision, or <see langword="null"/> when absent.</returns>
    Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int portfolioId, CancellationToken cancellationToken = default);

    /// <summary>Gets portfolio projections in an operating-state bucket.</summary>
    /// <param name="state">The operating state.</param>
    /// <param name="bucket">The persisted state bucket.</param>
    /// <param name="afterPortfolioId">The exclusive portfolio identifier cursor.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching portfolio projections.</returns>
    Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(
        PortfolioOperatingState state,
        int bucket,
        int afterPortfolioId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets fund projections owned by a portfolio.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="afterFundId">The exclusive fund identifier cursor.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching fund projections.</returns>
    Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(
        int portfolioId,
        int afterFundId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a fund projection.</summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The projection, or <see langword="null"/> when absent.</returns>
    Task<FundMandateReadModel?> GetFundAsync(int fundId, CancellationToken cancellationToken = default);

    /// <summary>Gets a fund projection revision.</summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The revision, or <see langword="null"/> when absent.</returns>
    Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int fundId, CancellationToken cancellationToken = default);

    /// <summary>Gets active fund projections matching the supplied decision horizon.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="tradingYear">The trading year.</param>
    /// <param name="decisionHorizon">The decision horizon.</param>
    /// <param name="effectiveAtUtc">The required effective UTC instant.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching active fund projections.</returns>
    Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(
        int portfolioId,
        int tradingYear,
        string decisionHorizon,
        DateTime effectiveAtUtc,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets effective trade-template assignments for selection.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="mandateVersion">The mandate version.</param>
    /// <param name="horizon">The decision horizon.</param>
    /// <param name="root">The underlying root.</param>
    /// <param name="asOfUtc">The effective UTC instant.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective assignments.</returns>
    Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(
        int portfolioId,
        int fundId,
        long mandateVersion,
        string horizon,
        string root,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Gets trade-template assignments for a fund mandate.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="mandateVersion">The mandate version.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching assignments.</returns>
    Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(
        int portfolioId,
        int fundId,
        long mandateVersion,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current fund allocation.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current allocation, or <see langword="null"/> when absent.</returns>
    Task<FundAllocationReadModel?> GetCurrentAllocationAsync(
        int portfolioId,
        int fundId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current fund risk envelope.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current risk envelope, or <see langword="null"/> when absent.</returns>
    Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(
        int portfolioId,
        int fundId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets order projections from a fund timeline.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderMonth">The order month.</param>
    /// <param name="beforeUtc">The exclusive UTC time cursor.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching order projections.</returns>
    Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(
        int portfolioId,
        int fundId,
        DateOnly orderMonth,
        DateTime beforeUtc,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets an order projection.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The projection, or <see langword="null"/> when absent.</returns>
    Task<FundOrderProjectionReadModel?> GetOrderAsync(int orderId, CancellationToken cancellationToken = default);

    /// <summary>Gets trade projections owned by an order.</summary>
    /// <param name="orderId">The owning order identifier.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching trade projections.</returns>
    Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(
        int orderId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a trade projection.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The projection, or <see langword="null"/> when absent.</returns>
    Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int tradeId, CancellationToken cancellationToken = default);

    /// <summary>Gets composition projections for a workflow.</summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching composition projections.</returns>
    Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(
        Guid workflowId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a financial-policy projection.</summary>
    /// <param name="policyId">The policy identifier.</param>
    /// <param name="policyVersion">The optional policy version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The projection, or <see langword="null"/> when absent.</returns>
    Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(
        int policyId,
        long? policyVersion = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets financial-policy projections for a portfolio.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="pageSize">The maximum result count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching policy projections.</returns>
    Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(
        int portfolioId,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the active financial-policy projection for a portfolio.</summary>
    /// <param name="portfolioId">The owning portfolio identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The active projection, or <see langword="null"/> when absent.</returns>
    Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(
        int portfolioId,
        CancellationToken cancellationToken = default);
}
