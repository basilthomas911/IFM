using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Defines Portfolio database commands.</summary>
public interface IPortfolioDbWriteContext
{
    /// <summary>Upserts a portfolio projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="stateBucket">The operating-state bucket.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertPortfolioAsync(
        PortfolioProjection<PortfolioReadModel> row,
        int stateBucket,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund mandate projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertFundAsync(
        PortfolioProjection<FundMandateReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund trade-template assignment projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertAssignmentAsync(
        PortfolioProjection<FundTradeTemplateAssignmentReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund allocation projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertAllocationAsync(
        PortfolioProjection<FundAllocationReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund risk-envelope projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertRiskEnvelopeAsync(
        PortfolioProjection<FundRiskEnvelopeReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a draft portfolio and its owned projections.</summary>
    /// <param name="deletion">The deletion description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task DeleteDraftPortfolioAsync(
        DraftPortfolioProjectionDeletion deletion,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund order projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="orderMonth">The order timeline month.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertOrderAsync(
        PortfolioProjection<FundOrderProjectionReadModel> row,
        DateOnly orderMonth,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund order trade projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertTradeAsync(
        PortfolioProjection<FundOrderTradeProjectionReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes an order projection and its subordinate trades.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="sourceEventId">The source event authorizing deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task DeleteOrderAsync(
        int orderId,
        long sourceEventId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a trade projection.</summary>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="sourceEventId">The source event authorizing deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task DeleteTradeAsync(
        int tradeId,
        long sourceEventId,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a fund composition workflow projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertCompositionAsync(
        PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts a financial-policy projection.</summary>
    /// <param name="row">The projection envelope.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task UpsertPolicyAsync(
        PortfolioProjection<PortfolioFinancialPolicyReadModel> row,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a draft financial-policy projection.</summary>
    /// <param name="deletion">The deletion description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous command.</returns>
    Task DeleteDraftPolicyAsync(
        DraftPolicyProjectionDeletion deletion,
        CancellationToken cancellationToken = default);
}
