



using TomasAI.IFM.Shared.EventSourcing;


namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

public interface IPortfolioAuthorityFence
{
    Task AppendAsync(int portfolioId, int? fundId, string stream, IEvent domainEvent, long expectedRevision, bool changesAuthority, CancellationToken token = default);
    /// <summary>Commits a source transition with its optional full checkpoint and upstream fence.</summary>
    Task AppendAsync(int portfolioId, PortfolioAuthorityAppend operation, CancellationToken token = default)
        => operation.Checkpoint is null && operation.ExpectedPortfolioRevision is null && operation.PreparedIdentifiers is null
            ? AppendAsync(portfolioId, operation.FundId, operation.Stream, operation.Source, operation.ExpectedRevision,
                operation.ChangesAuthority, token)
            : throw new NotSupportedException("Prepared Portfolio authority and checkpoints require a transactional fence.");
    /// <summary>Commits related accepted events and projections on a single owning transaction.</summary>
    Task AppendBatchAsync(int portfolioId, IReadOnlyList<PortfolioAuthorityAppend> operations, CancellationToken token = default)
        => throw new NotSupportedException("Compound Portfolio operations require a transactional fence.");
}
