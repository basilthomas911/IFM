using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;
/// <summary>Commits accepted contract events and canonical, symbol and reference projections together.</summary>
internal static class SecuritiesContractAtomicPersistence
{
    internal static async Task SaveAsync(ISecuritiesDbContext database, IPostgresEventTransaction transactions,
        DomainEventCollection pending, ICommand command, long committedVersion, CancellationToken token)
    {
        await transactions.ExecuteAsync(async (transaction,cancellation) =>
        {
            var version = committedVersion;
            foreach (var source in pending)
            {
                await transaction.AppendAsync(command.StreamId,command.CommandId,source,version++,cancellation).ConfigureAwait(false);
                await MaterializeAsync(database,transaction,source,cancellation).ConfigureAwait(false);
            }
            return true;
        },token).ConfigureAwait(false);
    }
    /// <summary>Materializes accepted facts without broker requests, notifications or commands.</summary>
    internal static async Task MaterializeAsync(ISecuritiesDbContext database, IEnlistedPostgresTransaction transaction,
        IEvent source, CancellationToken token)
    {
        switch (source)
        {
            case FuturesContractAddedEvent added:
                await database.ProjectFuturesContractAsync(transaction,added.Contract,token:token).ConfigureAwait(false); break;
            case FuturesContractChangedEvent changed:
                await database.ProjectFuturesContractAsync(transaction,changed.Contract,changed.OriginalContractId.ContractId,token).ConfigureAwait(false); break;
            case FuturesContractRemovedEvent removed:
                await database.RemoveFuturesContractAsync(transaction,removed.ContractId.ContractId,token).ConfigureAwait(false); break;
            case FuturesOptionContractAddedEvent added:
                await database.ProjectOptionContractAsync(transaction,added.Contract,token:token).ConfigureAwait(false); break;
            case FuturesOptionContractChangedEvent changed:
                await database.ProjectOptionContractAsync(transaction,changed.Contract,changed.OriginalContractId,token).ConfigureAwait(false); break;
            case FuturesOptionContractRemovedEvent removed:
                await database.RemoveOptionContractAsync(transaction,removed.ContractId,token).ConfigureAwait(false); break;
            case FuturesOptionContractsAddedEvent added:
                foreach (var contract in added.Contracts.OrderBy(value => value.ContractId,StringComparer.Ordinal))
                    await database.ProjectOptionContractAsync(transaction,contract,token:token).ConfigureAwait(false);
                break;
            default: throw new InvalidOperationException("Unexpected securities contract source event.");
        }
    }
}
