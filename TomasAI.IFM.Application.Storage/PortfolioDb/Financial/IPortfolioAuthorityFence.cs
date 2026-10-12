using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IPortfolioAuthorityFence
{
    Task AppendAsync(int portfolioId, int? fundId, string stream, IEvent domainEvent, long expectedRevision, bool changesAuthority, CancellationToken token = default);
    /// <summary>Commits related accepted events and projections on a single owning transaction.</summary>
    Task AppendBatchAsync(int portfolioId, IReadOnlyList<PortfolioAuthorityAppend> operations, CancellationToken token = default)
        => throw new NotSupportedException("Compound Portfolio operations require a transactional fence.");
}
