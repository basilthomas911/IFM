using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Prepares reference versions at the publisher boundary before calling the persistence adapter.</summary>
public static class ReferenceVersionWriter
{
    extension(ISecuritiesDbWriteContext database)
    {
        public Task ProjectFuturesContractAsync(IEnlistedPostgresTransaction transaction, FuturesContractV3ReadModel contract,
            string? originalContractId = null, CancellationToken token = default)
            => database.ProjectFuturesContractAsync(transaction, PreparedFuturesProjection.Prepare(contract), originalContractId, token);

        public Task ProjectOptionContractAsync(IEnlistedPostgresTransaction transaction, FuturesOptionContractReadModel contract,
            string? originalContractId = null, CancellationToken token = default)
            => database.ProjectOptionContractAsync(transaction, PreparedOptionProjection.Prepare(contract), originalContractId, token);

        public Task<PendingReferenceVersion> StageReferenceVersionAsync(FuturesContractV3ReadModel value,
            CancellationToken cancellationToken = default)
            => database.StageReferenceVersionAsync(PreparedReferenceVersion.Prepare(value), cancellationToken);

        public Task<PendingReferenceVersion> StageReferenceVersionAsync(FuturesOptionContractReadModel value,
            CancellationToken cancellationToken = default)
            => database.StageReferenceVersionAsync(PreparedReferenceVersion.Prepare(value), cancellationToken);
    }
}
