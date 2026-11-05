using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Rebuilds Securities query rows from exact committed facts.</summary>
public static class SecuritiesCatalogProjection
{
    /// <summary>Rebuilds an empty catalog projection from historical, baseline and new catalog events.</summary>
    public static Task<int> RebuildEmptyAsync(SecuritiesDbContext database, IPostgresEventTransaction transactions,
        CancellationToken cancellationToken = default) => transactions.ExecuteOnceAsync(async (transaction, token) =>
    {
        await transaction.ExecuteAsync("""
            LOCK TABLE public.event_stream_id, public.event_log,
                securities.futures_contract, securities.futures_contract_by_symbol,
                securities.futures_option_contract, securities.futures_option_contract_by_symbol
                IN SHARE ROW EXCLUSIVE MODE;
            """, [], token)
                .ConfigureAwait(false);
        if (await transaction.ScalarAsync("""
            SELECT EXISTS (SELECT FROM securities.futures_contract UNION ALL SELECT FROM securities.futures_contract_by_symbol
                UNION ALL SELECT FROM securities.futures_option_contract UNION ALL SELECT FROM securities.futures_option_contract_by_symbol);
            """, [], token)
                .ConfigureAwait(false) is true)
            throw new InvalidOperationException("Securities rebuild requires empty canonical and symbol tables.");

        var rows = await transaction.QueryAsync("""
            SELECT l.eventstreamid, n.eventname, n.eventtypename, l.eventversion, l.eventpayload,
                l.commandid, l.eventtimestamp, l.streamversion, s.eventstream
            FROM public.event_stream_id s
            CROSS JOIN LATERAL (SELECT * FROM public.event_log l WHERE l.eventstreamid = s.eventstreamid OFFSET 0) l
            JOIN public.event_name_id n ON n.eventnameid = l.eventnameid
            WHERE s.eventstream = $1 OR starts_with(s.eventstream, 'Command.FuturesContractCommand.')
                OR starts_with(s.eventstream, 'Command.FuturesOptionContractCommand.')
            ORDER BY l.eventversion;
            """, [SecuritiesCatalogStreams.Migration], static row =>
                (Event: EventSourceActorDbContext.MapToEventLog(new AdoNetDataRecord().SetReader(row)), Stream: row.GetString(8)),
            token)
                .ConfigureAwait(false);
        var baseline = rows.SingleOrDefault(static row => row.Stream == SecuritiesCatalogStreams.Migration)
            .Event?.ToDomainEvent() as SecuritiesCatalogBaselineEvent
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
                await database.RemoveFuturesContractAsync(transaction,removed.ContractId.ContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractAddedEvent added:
                await database.ProjectOptionContractAsync(transaction,added.Contract,token:token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractChangedEvent changed:
                await database.ProjectOptionContractAsync(transaction,changed.Contract,changed.OriginalContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractRemovedEvent removed:
                await database.RemoveOptionContractAsync(transaction,removed.ContractId,token)
                    .ConfigureAwait(false); break;
            case FuturesOptionContractsAddedEvent added:
                foreach (var contract in added.Contracts.OrderBy(value => value.ContractId,StringComparer.Ordinal))
                    await database.ProjectOptionContractAsync(transaction,contract,token:token)
                        .ConfigureAwait(false);
                break;
            case FuturesContractRolloverSetReplacedEvent rollover when database is SecuritiesDbContext postgres:
                await SecuritiesFuturesCatalog.ApplyAsync(postgres, transaction, rollover, token)
                    .ConfigureAwait(false);
                break;
            case SecuritiesCatalogBaselineEvent baseline:
                foreach (var contract in baseline.FuturesWithoutHistory)
                    await database.ProjectFuturesContractAsync(transaction, contract, token: token)
                        .ConfigureAwait(false);
                foreach (var contract in baseline.OptionsWithoutHistory)
                    await database.ProjectOptionContractAsync(transaction, contract, token: token)
                        .ConfigureAwait(false);
                foreach (var pointer in baseline.RolloverPointers)
                    await SecuritiesFuturesCatalog.ApplyPointerAsync(transaction, pointer, token)
                        .ConfigureAwait(false);
                break;
            case FuturesContractStateSnapshottedEvent or FuturesOptionContractStateSnapshottedEvent:
                break;
            default: throw new InvalidOperationException("Unexpected securities contract source event.");
        }
    }
}
