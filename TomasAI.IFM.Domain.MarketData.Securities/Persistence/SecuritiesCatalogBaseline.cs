using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Reconciles legacy Securities streams into two catalog creation snapshots.</summary>
/// <param name="transactions">The source transaction owner.</param>
/// <param name="database">Typed canonical-row and source-history persistence.</param>
/// <param name="timeProvider">The application clock used when baseline events are created.</param>
public sealed class SecuritiesCatalogBaseline(IPostgresEventTransaction transactions, ISecuritiesDbContext database, TimeProvider? timeProvider = null)
{
    /// <summary>Captures the cutover once after the owning writers have drained.</summary>
    public Task<(int Futures, int Options)?> CaptureAsync(bool writersDrained,
        CancellationToken cancellationToken = default, (int Futures, int Options)? expectedMissing = null)
    {
        if (!writersDrained)
            throw new InvalidOperationException("Drain all Securities writers before consolidating their streams.");

        return transactions.ExecuteOnceAsync<(int Futures, int Options)?>(async (transaction, token) =>
        {
            await database.LockCatalogMaintenanceAsync(transaction, includeSymbolTables: false, token).ConfigureAwait(false);
            if (await database.HasCatalogSourceStreamAsync(transaction, SecuritiesCatalogStreams.Migration, token)
                .ConfigureAwait(false))
                return null;
            if (await database.HasCatalogSourceStreamAsync(transaction, SecuritiesCatalogStreams.Futures, token)
                .ConfigureAwait(false)
                || await database.HasCatalogSourceStreamAsync(transaction, SecuritiesCatalogStreams.Options, token)
                    .ConfigureAwait(false))
                throw new InvalidOperationException("A consolidated Securities stream already has writes without a baseline.");

            var captured = await database.ReadCatalogRowsAsync(transaction, token).ConfigureAwait(false);
            var futures = captured.Futures;
            var options = captured.Options;
            var rollover = captured.RolloverPointers;
            var sources = await database.ReadCatalogSourcesAsync(transaction, [],
                ["Command.FuturesContractCommand.", "Command.FuturesOptionContractCommand."], token).ConfigureAwait(false);
            IReadOnlyList<EventLogReadModel> history = sources.Select(row => row.Event).ToArray();

            var futuresHistory = new Dictionary<FuturesContractId, FuturesContractV3ReadModel>();
            var optionsHistory = new Dictionary<string, FuturesOptionContractReadModel>(StringComparer.Ordinal);
            foreach (var row in history)
            {
                switch (row.ToDomainEvent())
                {
                    case FuturesContractAddedEvent added:
                        futuresHistory[added.Contract.Id] = added.Contract;
                        break;
                    case FuturesContractChangedEvent changed:
                        futuresHistory.Remove(changed.OriginalContractId);
                        futuresHistory[changed.Contract.Id] = changed.Contract;
                        break;
                    case FuturesContractRemovedEvent removed:
                        futuresHistory.Remove(removed.ContractId);
                        break;
                    case FuturesOptionContractAddedEvent added:
                        optionsHistory[added.Contract.ContractId] = added.Contract;
                        break;
                    case FuturesOptionContractsAddedEvent added:
                        foreach (var contract in added.Contracts)
                            optionsHistory[contract.ContractId] = contract;
                        break;
                    case FuturesOptionContractChangedEvent changed:
                        optionsHistory.Remove(changed.OriginalContractId);
                        optionsHistory[changed.Contract.ContractId] = changed.Contract;
                        break;
                    case FuturesOptionContractRemovedEvent removed:
                        optionsHistory.Remove(removed.ContractId);
                        break;
                    case FuturesContractStateSnapshottedEvent or FuturesOptionContractStateSnapshottedEvent:
                        break;
                    default:
                        throw new InvalidDataException($"Cannot replay Securities event {row.EventVersion} ({row.EventName}).");
                }
            }

            var futuresById = futures.ToDictionary(static contract => contract.Id);
            var optionsById = options.ToDictionary(static contract => contract.ContractId, StringComparer.Ordinal);
            VerifyProjection(futuresHistory, futuresById, "futures");
            VerifyProjection(optionsHistory, optionsById, "options");
            var missingFutures = futures.Where(contract => !futuresHistory.ContainsKey(contract.Id)).ToArray();
            var missingOptions = options.Where(contract => !optionsHistory.ContainsKey(contract.ContractId)).ToArray();
            if (expectedMissing is { } expected && (missingFutures.Length != expected.Futures || missingOptions.Length != expected.Options))
                throw new InvalidDataException($"The verified Securities baseline expected {expected.Futures} futures and {expected.Options} options without history. Found {missingFutures.Length} futures and {missingOptions.Length} options.");
            var id = Guid.NewGuid();
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            await transaction.AppendAsync(new EnlistedEventAppend(SecuritiesCatalogStreams.Futures, id, new FuturesContractStateSnapshottedEvent
            {
                Id = Guid.NewGuid(), CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, FuturesContractAddedEvent.Actor, "StateSnapshotted", SecuritiesCatalogStreams.Entity),
                ContractIds = futures.Select(static contract => contract.Id).ToArray()
            }, 0), token)
                .ConfigureAwait(false);
            await transaction.AppendAsync(new EnlistedEventAppend(SecuritiesCatalogStreams.Options, id, new FuturesOptionContractStateSnapshottedEvent
            {
                Id = Guid.NewGuid(), CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, FuturesOptionContractAddedEvent.Actor, "StateSnapshotted", SecuritiesCatalogStreams.Entity),
                ContractIds = options.Select(static contract => contract.ContractId).ToArray()
            }, 0), token)
                .ConfigureAwait(false);
            await transaction.AppendAsync(new EnlistedEventAppend(SecuritiesCatalogStreams.Migration, id, new SecuritiesCatalogBaselineEvent
            {
                Id = id, CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, "SecuritiesMigration", "PostgresBaseline", "v1"),
                LegacyThroughEventId = history.Count == 0 ? 0 : history[^1].EventVersion,
                FuturesWithoutHistory = missingFutures, OptionsWithoutHistory = missingOptions,
                RolloverPointers = rollover.ToArray()
            }, 0), token)
                .ConfigureAwait(false);
            return (missingFutures.Length, missingOptions.Length);
        }, cancellationToken);
    }

    static void VerifyProjection<TKey, TContract>(Dictionary<TKey, TContract> history,
        Dictionary<TKey, TContract> canonical, string catalog) where TKey : notnull
    {
        var missing = history.Keys.Count(key => !canonical.ContainsKey(key));
        var changed = history.Count(entry => canonical.TryGetValue(entry.Key, out var row)
            && !EqualityComparer<TContract>.Default.Equals(entry.Value, row));
        if (missing > 0 || changed > 0)
            throw new InvalidDataException($"The {catalog} catalog has {missing} replayed contracts missing from PostgreSQL and {changed} payload differences.");
    }
}
