



using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public interface IPortfolioAuthorityFence
{
    Task AppendAsync(int portfolioId, int? fundId, string stream, IEvent domainEvent, long expectedRevision, bool changesAuthority, CancellationToken token = default);
    /// <summary>Commits related accepted events and projections on a single owning transaction.</summary>
    Task AppendBatchAsync(int portfolioId, IReadOnlyList<PortfolioAuthorityAppend> operations, CancellationToken token = default)
        => throw new NotSupportedException("Compound Portfolio operations require a transactional fence.");
}
