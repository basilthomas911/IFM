using System.Globalization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Enlists the market data host's writes in the futures catalog stream.</summary>
internal static class SecuritiesFuturesCatalog
{
    internal static Task<bool> TryImportDefinitionAsync(SecuritiesDbContext database, FuturesContractV3ReadModel contract)
        => ExecuteAsync(database, async (transaction, version, token) =>
        {
            var existing = await transaction.QueryAsync(SecuritiesDbSql.GetFuturesContract, [contract.ContractId],
                static row => SecuritiesDbContext.MapToFuturesContract(new AdoNetDataRecord().SetReader(row)), token)
                    .ConfigureAwait(false);
            var current = existing.SingleOrDefault();
            if (current == contract)
                return;
            if (current is not null && current.Id != contract.Id)
                throw new InvalidDataException($"Futures definition '{contract.ContractId}' conflicts with its catalog identity.");

            var id = Guid.NewGuid();
            var fact = new FuturesContractDefinitionImportedEvent
            {
                Id = id, CommandId = id, AggregateId = SecuritiesCatalogStreams.Futures,
                ReceivedOn = DateTime.UtcNow, EventSource = nameof(SecuritiesFuturesCatalog),
                Subject = new(ActorType.Event, "FuturesContractEvent", "DefinitionImported", contract.ContractId),
                Contract = contract
            };
            await transaction.AppendAsync(SecuritiesCatalogStreams.Futures, id, fact, version, token)
                .ConfigureAwait(false);
            await database.ProjectFuturesContractAsync(transaction, contract, token: token)
                .ConfigureAwait(false);
        }, CancellationToken.None);

    internal static Task<bool> TryReplaceAsync(SecuritiesDbContext database, FuturesContractRolloverReadModel rollover,
        FuturesContractV3ReadModel[] replacements, string symbol, CancellationToken cancellationToken)
        => ExecuteAsync(database, async (transaction, version, token) =>
        {
            var existing = await transaction.QueryAsync(SecuritiesDbSql.GetRolloverFuturesContracts, [symbol],
                static row => SecuritiesDbContext.MapToFuturesContract(new AdoNetDataRecord().SetReader(row)), token)
                .ConfigureAwait(false);
            var activeIds = replacements.Select(static contract => contract.Id).ToHashSet();
            var retired = existing.Where(contract => !activeIds.Contains(contract.Id))
                .Select(static contract => contract with { OnTheRun = false, Rollover = false });
            var id = Guid.NewGuid();
            var fact = new FuturesContractRolloverSetReplacedEvent
            {
                Id = id, CommandId = id, AggregateId = SecuritiesCatalogStreams.Futures,
                ReceivedOn = DateTime.UtcNow, EventSource = nameof(SecuritiesFuturesCatalog),
                Subject = new(ActorType.Event, "FuturesContractEvent", "RolloverSetReplaced", symbol),
                Rollover = rollover with { Symbol = symbol },
                ChangedContracts = [.. retired, .. replacements]
            };
            await transaction.AppendAsync(SecuritiesCatalogStreams.Futures, id, fact, version, token)
                .ConfigureAwait(false);
            await ApplyAsync(database, transaction, fact, token)
                .ConfigureAwait(false);
        }, cancellationToken);

    static Task<bool> ExecuteAsync(SecuritiesDbContext database,
        Func<EnlistedEventTransaction, long, CancellationToken, Task> apply, CancellationToken cancellationToken)
    {
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            database.ConnectionString, database.ProviderName);
        return new PostgresEventTransaction(settings).ExecuteOnceAsync(async (transaction, token) =>
        {
            if (await transaction.ScalarAsync("SELECT to_regclass('public.event_stream_id')::text;", [], token)
                    .ConfigureAwait(false) is null)
                return false;
            var version = Convert.ToInt64(await transaction.ScalarAsync("""
                SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1 FOR UPDATE;
                """, [SecuritiesCatalogStreams.Futures], token)
                    .ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (version == 0)
                return false;
            await apply(transaction, version, token)
                .ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    /// <summary>Materializes a committed rollover fact from its recorded rows.</summary>
    public static async Task ApplyAsync(SecuritiesDbContext database, IEnlistedPostgresTransaction transaction,
        FuturesContractRolloverSetReplacedEvent fact, CancellationToken token)
    {
        foreach (var contract in fact.ChangedContracts)
        {
            var parameters = (object?[])database.ToInsertParameters(contract).Bind();
            await database.RemoveFuturesContractAsync(transaction, contract.ContractId, token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContract, parameters, token)
                .ConfigureAwait(false);
            await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContractBySymbolV3, parameters, token)
                .ConfigureAwait(false);
        }

        await ApplyPointerAsync(transaction, fact.Rollover, token)
            .ConfigureAwait(false);
    }

    internal static async Task ApplyPointerAsync(IEnlistedPostgresTransaction transaction,
        FuturesContractRolloverReadModel rollover, CancellationToken token)
    {
        await transaction.ExecuteAsync(SecuritiesDbSql.InsertFuturesContractRolloverIfMissing,
            [rollover.Symbol, rollover.CreatedOn, rollover.CreatedBy], token)
                .ConfigureAwait(false);
        if (rollover.ContractId is null || rollover.NextRolloverDate is not { } date
            || rollover.UpdatedOn is not { } updated || rollover.UpdatedBy is not { } updatedBy)
            return;
        var pointer = new UpdateFuturesContractRollover(rollover.ContractId, date, updated, updatedBy, rollover.Symbol);
        if (await transaction.ExecuteAsync(SecuritiesDbSql.UpdateFuturesContractRollover,
                (object?[])pointer.Bind(), token)
                    .ConfigureAwait(false) != 1)
            throw new InvalidOperationException($"The {rollover.Symbol} rollover pointer does not exist.");
    }
}
