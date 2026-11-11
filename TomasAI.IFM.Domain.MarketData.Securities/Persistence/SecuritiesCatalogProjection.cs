using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Rebuilds Securities query rows from exact committed facts.</summary>
public static class SecuritiesCatalogProjection
{
    /// <summary>Rebuilds an empty catalog projection from historical, baseline and new catalog events.</summary>
    public static Task<int> RebuildEmptyAsync(ISecuritiesDbContext database, IPostgresEventTransaction transactions,
        CancellationToken cancellationToken = default) => transactions.ExecuteOnceAsync(async (transaction, token) =>
    {
        await database.LockCatalogMaintenanceAsync(transaction, includeSymbolTables: true, token).ConfigureAwait(false);
        if (await database.CatalogProjectionHasRowsAsync(transaction, token).ConfigureAwait(false))
            throw new InvalidOperationException("Securities rebuild requires empty canonical and symbol tables.");
        var rows = await database.ReadCatalogSourcesAsync(transaction, [SecuritiesCatalogStreams.Migration],
            ["Command.FuturesContractCommand.", "Command.FuturesOptionContractCommand."], token).ConfigureAwait(false);
        var baseline = rows.SingleOrDefault(static row => row.Stream == SecuritiesCatalogStreams.Migration)
            ?.Event.ToDomainEvent() as SecuritiesCatalogBaselineEvent
            ?? throw new InvalidOperationException("Securities rebuild requires the verified catalog baseline.");

        var applied = 0;
        foreach (var row in rows)
        {
            if (row.Stream is not (SecuritiesCatalogStreams.Futures or SecuritiesCatalogStreams.Options or SecuritiesCatalogStreams.Migration)
                && row.Event.EventVersion > baseline.LegacyThroughEventId)
                throw new InvalidDataException($"Legacy Securities stream '{row.Stream}' received a write after cutover.");
            var source = row.Event.ToDomainEvent();
            if (source is FuturesContractStateSnapshottedEvent or FuturesOptionContractStateSnapshottedEvent)
                continue;
            await ApplyAsync(database, transaction, source, token)
                .ConfigureAwait(false);
            applied++;
        }
        return applied;
    }, cancellationToken);

    /// <summary>Materializes accepted facts without broker requests, notifications or commands.</summary>
    public static async Task ApplyAsync(ISecuritiesDbContext database, IEnlistedPostgresTransaction transaction,
        IEvent source, CancellationToken token)
    {
        switch (source)
        {
            case FuturesContractAddedEvent added:
                await database.ProjectFuturesContractAsync(transaction,added.Contract,token:token)
                    .ConfigureAwait(false); break;
            case FuturesContractDefinitionImportedEvent imported:
                await database.ProjectFuturesContractAsync(transaction, imported.Contract, token: token)
                    .ConfigureAwait(false);
                break;
            case FuturesContractChangedEvent changed:
                await database.ProjectFuturesContractAsync(transaction,changed.Contract,changed.OriginalContractId.ContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesContractRemovedEvent removed:
                ArgumentException.ThrowIfNullOrWhiteSpace(removed.ContractId.ContractId);
                await database.RemoveFuturesContractAsync(transaction,removed.ContractId.ContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractAddedEvent added:
                await database.ProjectOptionContractAsync(transaction,added.Contract,token:token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractChangedEvent changed:
                await database.ProjectOptionContractAsync(transaction,changed.Contract,changed.OriginalContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractRemovedEvent removed:
                ArgumentException.ThrowIfNullOrWhiteSpace(removed.ContractId);
                await database.RemoveOptionContractAsync(transaction,removed.ContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractsAddedEvent added:
                foreach (var contract in added.Contracts.OrderBy(value => value.ContractId,StringComparer.Ordinal))
                    await database.ProjectOptionContractAsync(transaction,contract,token:token)
                        .ConfigureAwait(false);
                break;
            case FuturesContractRolloverSetReplacedEvent rollover:
                await database.WriteFuturesCatalogRowsAsync(transaction, rollover.ChangedContracts, token)
                    .ConfigureAwait(false);
                await database.WriteFuturesRolloverPointerAsync(transaction, rollover.Rollover, token).ConfigureAwait(false);
                break;
            case SecuritiesCatalogBaselineEvent baseline:
                foreach (var contract in baseline.FuturesWithoutHistory)
                    await database.ProjectFuturesContractAsync(transaction, contract, token: token)
                        .ConfigureAwait(false);
                foreach (var contract in baseline.OptionsWithoutHistory)
                    await database.ProjectOptionContractAsync(transaction, contract, token: token)
                        .ConfigureAwait(false);
                foreach (var pointer in baseline.RolloverPointers)
                    await database.WriteFuturesRolloverPointerAsync(transaction, pointer, token)
                        .ConfigureAwait(false);
                break;
            case FuturesContractStateSnapshottedEvent or FuturesOptionContractStateSnapshottedEvent:
                break;
            default: throw new InvalidOperationException("Unexpected securities contract source event.");
        }
    }
}
