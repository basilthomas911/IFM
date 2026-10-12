using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IGeneralLedgerStore
{
    Task<T> PostAsync<T>(IFinancialRequest request, IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate, Func<LedgerCommitInfo, T> complete, CancellationToken token = default) where T : class, IFinancialCompletedEvent;
}
