using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Reconciles legacy Securities streams into two catalog creation snapshots.</summary>
public sealed class SecuritiesCatalogBaseline(IPostgresEventTransaction transactions)
{
    /// <summary>Captures the cutover once after the owning writers have drained.</summary>
    public Task<(int Futures, int Options)?> CaptureAsync(bool writersDrained,
        CancellationToken cancellationToken = default, (int Futures, int Options)? expectedMissing = null)
    {
        if (!writersDrained)
            throw new InvalidOperationException("Drain all Securities writers before consolidating their streams.");

        return transactions.ExecuteOnceAsync<(int Futures, int Options)?>(async (transaction, token) =>
        {
            await transaction.ExecuteAsync("""
                LOCK TABLE public.event_stream_id, public.event_log,
                    securities.futures_contract, securities.futures_option_contract, securities.futures_contract_rollover
                    IN SHARE ROW EXCLUSIVE MODE;
                """, [], token)
                    .ConfigureAwait(false);
            if (await HasStreamAsync(transaction, SecuritiesCatalogStreams.Migration, token)
                .ConfigureAwait(false))
                return null;
            if (await HasStreamAsync(transaction, SecuritiesCatalogStreams.Futures, token)
                .ConfigureAwait(false)
                || await HasStreamAsync(transaction, SecuritiesCatalogStreams.Options, token)
                    .ConfigureAwait(false))
                throw new InvalidOperationException("A consolidated Securities stream already has writes without a baseline.");

            var futures = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesContracts, [],
                static row => SecuritiesDbContext.MapToFuturesContract(new AdoNetDataRecord().SetReader(row)), token)
                .ConfigureAwait(false);
            var options = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesOptionContracts, [],
                static row => SecuritiesDbContext.MapToFuturesOptionContract(new AdoNetDataRecord().SetReader(row)), token)
                .ConfigureAwait(false);
            var rollover = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesContractRollovers, [],
                static row => SecuritiesDbContext.MapToFuturesContractRollover(new AdoNetDataRecord().SetReader(row)), token)
                .ConfigureAwait(false);
            var history = await transaction.QueryAsync("""
                SELECT l.eventstreamid, n.eventname, n.eventtypename, l.eventversion, l.eventpayload,
                    l.commandid, l.eventtimestamp, l.streamversion
                FROM public.event_stream_id s
                CROSS JOIN LATERAL (SELECT * FROM public.event_log l WHERE l.eventstreamid = s.eventstreamid OFFSET 0) l
                JOIN public.event_name_id n ON n.eventnameid = l.eventnameid
                WHERE starts_with(s.eventstream, $1) OR starts_with(s.eventstream, $2)
                ORDER BY l.eventversion;
                """, ["Command.FuturesContractCommand.", "Command.FuturesOptionContractCommand."],
                static row => EventSourceActorDbContext.MapToEventLog(new AdoNetDataRecord().SetReader(row)), token)
                .ConfigureAwait(false);

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
            var now = DateTime.UtcNow;
            await transaction.AppendAsync(SecuritiesCatalogStreams.Futures, id, new FuturesContractStateSnapshottedEvent
            {
                Id = Guid.NewGuid(), CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, FuturesContractAddedEvent.Actor, "StateSnapshotted", SecuritiesCatalogStreams.Entity),
                ContractIds = futures.Select(static contract => contract.Id).ToArray()
            }, 0, token)
                .ConfigureAwait(false);
            await transaction.AppendAsync(SecuritiesCatalogStreams.Options, id, new FuturesOptionContractStateSnapshottedEvent
            {
                Id = Guid.NewGuid(), CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, FuturesOptionContractAddedEvent.Actor, "StateSnapshotted", SecuritiesCatalogStreams.Entity),
                ContractIds = options.Select(static contract => contract.ContractId).ToArray()
            }, 0, token)
                .ConfigureAwait(false);
            await transaction.AppendAsync(SecuritiesCatalogStreams.Migration, id, new SecuritiesCatalogBaselineEvent
            {
                Id = id, CommandId = id, ReceivedOn = now, EventSource = nameof(SecuritiesCatalogBaseline),
                Subject = new(ActorType.Event, "SecuritiesMigration", "PostgresBaseline", "v1"),
                LegacyThroughEventId = history.Count == 0 ? 0 : history[^1].EventVersion,
                FuturesWithoutHistory = missingFutures, OptionsWithoutHistory = missingOptions,
                RolloverPointers = rollover.ToArray()
            }, 0, token)
                .ConfigureAwait(false);
            return (missingFutures.Length, missingOptions.Length);
        }, cancellationToken);
    }

    static async Task<bool> HasStreamAsync(EnlistedEventTransaction transaction, string stream, CancellationToken token) =>
        await transaction.ScalarAsync(
            "SELECT currentversion FROM public.event_stream_id WHERE eventstream = $1 AND currentversion > 0;",
            [stream], token)
                .ConfigureAwait(false) is not null;

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
