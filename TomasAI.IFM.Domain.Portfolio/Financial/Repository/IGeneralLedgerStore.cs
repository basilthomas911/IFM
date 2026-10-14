


using TomasAI.IFM.Domain.Portfolio.Shared.Financial;



namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public interface IGeneralLedgerStore
{
    Task<T> PostAsync<T>(IFinancialRequest request, IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate, Func<LedgerCommitInfo, T> complete, CancellationToken token = default) where T : class, IFinancialCompletedEvent;
}
