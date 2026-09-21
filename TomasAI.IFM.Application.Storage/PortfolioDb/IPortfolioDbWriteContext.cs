using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public interface IPortfolioDbWriteContext
{
    // Portfolio and Fund
    Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row,int stateBucket,CancellationToken cancellationToken=default);
    Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row,CancellationToken cancellationToken=default);
    Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row,CancellationToken cancellationToken=default);
    Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row,CancellationToken cancellationToken=default);
    Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row,CancellationToken cancellationToken=default);
    Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion,CancellationToken cancellationToken=default);

    // Order composition and trade order
    Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row,DateOnly orderMonth,CancellationToken cancellationToken=default);
    Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row,CancellationToken cancellationToken=default);
    /// <summary>Deletes an order projection and any subordinate trade projections.</summary>
    Task DeleteOrderAsync(int orderId,long sourceEventId,CancellationToken cancellationToken=default);
    /// <summary>Deletes a trade projection when the supplied source event is not older than the stored row.</summary>
    Task DeleteTradeAsync(int tradeId,long sourceEventId,CancellationToken cancellationToken=default);
    Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row,CancellationToken cancellationToken=default);

    // Financial policy
    Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row,CancellationToken cancellationToken=default);
    Task DeleteDraftPolicyAsync(DraftPolicyProjectionDeletion deletion,CancellationToken cancellationToken=default);
}
