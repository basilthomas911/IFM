using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class SecuritiesCatalogBaselineTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Baseline_preserves_legacy_rows_and_rebuilds_with_post_cutover_rollover_and_option_tail()
    {
        await using var isolated = await PostgresInstrumentTestDatabase.CreateAsync(fixture.Connection);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, isolated.Connection, "System.Data.Postgres")
            .Add(SecuritiesDbContext.SecuritiesDbConnection, isolated.Connection, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
        await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
        var factory = Substitute.For<IDbContextFactory>();
        var database = new SecuritiesDbContext(settings, factory, logger);
        factory.SecuritiesDb.Returns(database);
        var transactions = new PostgresEventTransaction(settings);
        var expiry = new DateOnly(2026, 12, 18);
        var future = new FuturesContractV3ReadModel("ES20261218", "baseline", "ES", "ESZ6", "FUT", "USD", "CME", "50",
            expiry, true) { Rollover = true };
        var historical = new FuturesOptionContractReadModel("ES20261218C5000", "history", "ES", "ESZ6 C5000",
            "FOP", "USD", "CME", "50", expiry, 5000, "Call");
        var projectionOnly = historical with { ContractId = "ES20261218C5010", LocalSymbol = "ESZ6 C5010", StrikePrice = 5010 };
        var legacy = "Command.FuturesOptionContractCommand.ES20261218C5000.2026";
        var addId = Guid.NewGuid();
        await transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.AppendAsync(legacy, addId, new FuturesOptionContractAddedEvent
            {
                Id = addId, CommandId = addId, Contract = historical, EntityId = new(historical.ContractId, 2026),
                Subject = new(ActorType.Event, FuturesOptionContractAddedEvent.Actor, FuturesOptionContractAddedEvent.Verb, "historical")
            }, 0, ct);
            await database.ProjectOptionContractAsync(db, historical, token: ct);
            await database.ProjectOptionContractAsync(db, projectionOnly, token: ct);
            await database.ProjectFuturesContractAsync(db, future, token: ct);
            return true;
        }, token);

        var now = DateTime.UtcNow;
        await database.EnsureFuturesContractRolloverRowsAsync(["ES", "VX"], now, nameof(SecuritiesCatalogBaselineTests), token);
        var migration = new SecuritiesCatalogBaseline(transactions);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            migration.CaptureAsync(writersDrained: true, token, expectedMissing: (0, 1)));
        Assert.Equal((1, 1), await migration.CaptureAsync(writersDrained: true, token, expectedMissing: (1, 1)));
        Assert.Null(await migration.CaptureAsync(writersDrained: true, token));
        var current = await transactions.ExecuteOnceAsync((db, ct) => db.ScalarAsync(
            "SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1;", [legacy], ct), token);
        Assert.Equal(1L, current);

        var removeId = Guid.NewGuid();
        await transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            var removed = new FuturesOptionContractRemovedEvent
            {
                Id = removeId, CommandId = removeId, ContractId = projectionOnly.ContractId,
                EntityId = new(projectionOnly.ContractId, 2026),
                Subject = new(ActorType.Event, FuturesOptionContractRemovedEvent.Actor, FuturesOptionContractRemovedEvent.Verb, projectionOnly.ContractId)
            };
            await db.AppendAsync(SecuritiesCatalogStreams.Options, removeId, removed, 1, ct);
            await SecuritiesCatalogProjection.ApplyAsync(database, db, removed, ct);
            return true;
        }, token);
        var imported = future with { ContractId = "ES20270618", LastTradeDate = expiry.AddMonths(6), LocalSymbol = "ESM7", Rollover = false };
        await database.InsertFuturesContractAsync(imported);
        await database.InsertFuturesContractAsync(imported);
        var newExpiry = expiry.AddMonths(3);
        var replacement = future with { ContractId = "ES20270318", LastTradeDate = newExpiry, LocalSymbol = "ESH7" };
        await database.ReplaceFuturesRolloverSetAsync(new FuturesContractRolloverReadModel
        {
            Symbol = "ES", ContractId = replacement.ContractId, NextRolloverDate = newExpiry,
            CreatedOn = now, CreatedBy = "test", UpdatedOn = now, UpdatedBy = "test"
        }, [replacement], token);

        await transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync("""
            DELETE FROM securities.futures_contract_by_symbol;
            DELETE FROM securities.futures_contract;
            DELETE FROM securities.futures_option_contract_by_symbol;
            DELETE FROM securities.futures_option_contract;
            DELETE FROM securities.futures_contract_rollover;
            """, [], ct), token);
        Assert.Equal(5, await SecuritiesCatalogProjection.RebuildEmptyAsync(database, transactions, token));
        Assert.Equal(historical, await database.GetFuturesOptionContractAsync(historical.ContractId, token));
        Assert.Null(await database.GetFuturesOptionContractAsync(projectionOnly.ContractId, token));
        Assert.Equal(replacement, Assert.Single(await database.GetFuturesRolloverSetAsync("ES", token)));
        Assert.Equal(imported, await database.GetFuturesContractAsync(imported.ContractId, token));
        Assert.Equal(replacement.ContractId, (await database.GetFuturesContractRolloverAsync("ES", token))?.ContractId);
        Assert.NotNull(await database.GetFuturesContractRolloverAsync("VX", token));
        Assert.False((await database.GetFuturesContractAsync(future.ContractId, token))!.Rollover);
    }
}
