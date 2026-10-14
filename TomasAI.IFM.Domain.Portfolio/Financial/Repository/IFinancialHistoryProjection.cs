
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;








namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public interface IFinancialHistoryProjection
{
    Task ApplyAsync(IFinancialCompletedEvent completed, CancellationToken token = default);
}
