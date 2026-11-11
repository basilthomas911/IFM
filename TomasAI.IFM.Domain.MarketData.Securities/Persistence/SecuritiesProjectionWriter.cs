using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Prepares accepted legacy contract batches before writing their projections.</summary>
public static class SecuritiesProjectionWriter
{
    extension(ISecuritiesDbWriteContext database)
    {
        public async Task InsertQualifiedFuturesContractsAsync(ICollection<FuturesContractV3ReadModel> contracts)
        {
            RequireFutures(contracts);
            await database.InsertFuturesContractsAsync(contracts).ConfigureAwait(false);
        }

        public async Task InsertQualifiedFuturesContractAsync(FuturesContractV3ReadModel contract)
        {
            ArgumentNullException.ThrowIfNull(contract);
            RequireFutures([contract]);
            await database.InsertFuturesContractAsync(contract).ConfigureAwait(false);
        }

        public async Task InsertQualifiedOptionContractsAsync(ICollection<FuturesOptionContractReadModel> contracts)
        {
            ArgumentNullException.ThrowIfNull(contracts);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contract in contracts)
                if (!ids.Add(contract.ContractId))
                    throw new ArgumentException($"The futures-option write contains duplicate or ambiguous contractId '{contract.ContractId}'.", nameof(contracts));
            await database.InsertFuturesOptionContractsAsync(contracts).ConfigureAwait(false);
        }
    }

    static void RequireFutures(IEnumerable<FuturesContractV3ReadModel> contracts)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            if (contract.OnTheRun && !contract.Rollover)
                throw new ArgumentException($"Futures contract '{contract.ContractId}' cannot be on-the-run without belonging to the rollover set.", nameof(contracts));
            if (!ids.Add(contract.ContractId))
                throw new ArgumentException($"The futures-contract write contains duplicate or ambiguous contractId '{contract.ContractId}'.", nameof(contracts));
        }
    }
}
