using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

public sealed class FuturesOptionContractPagingTests
{
    [Fact]
    public async Task Scylla_pages_match_full_query_and_reject_invalid_or_stale_cursors()
    {
        var keyspace = "ifm_opg_" + Guid.NewGuid().ToString("N");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var token = timeout.Token;
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add(SecuritiesDbContext.SecuritiesDbConnection, $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Admin(settings["admin"], logger);
        await admin.Use("OptionPages.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            await new SecuritiesSchemaDb(settings, logger).CreateAllAsync();
            var factory = Substitute.For<IDbContextFactory>();
            var db = new SecuritiesDbContext(settings, factory, logger);
            factory.SecuritiesDb.Returns(db);
            var contracts = Enumerable.Range(0, 405).Select(i => Contract("ES", i)).ToArray();
            await db.InsertFuturesOptionContractsAsync(contracts);
            await db.InsertFuturesOptionContractAsync(Contract("NQ", 1));

            // The legacy query remains available and performs its existing projection repair.
            var full = (await db.GetFuturesOptionContractsAsync("ES", token)).ToArray();
            var first = await db.GetFuturesOptionContractsPageAsync(new("ES"), token);
            Assert.Equal(200, first.Items.Length);
            Assert.True(first.HasMore);
            var second = await db.GetFuturesOptionContractsPageAsync(new("ES", 200, first.ContinuationToken), token);
            Assert.Equal(200, second.Items.Length);
            var last = await db.GetFuturesOptionContractsPageAsync(new("ES", 200, second.ContinuationToken), token);
            Assert.Equal(5, last.Items.Length);
            Assert.False(last.HasMore);
            var allPages = first.Items.Concat(second.Items).Concat(last.Items).ToArray();
            Assert.Equal(full.Select(c => System.Text.Json.JsonSerializer.Serialize(c)), allPages.Select(c => System.Text.Json.JsonSerializer.Serialize(c)));
            Assert.Equal(405, allPages.Select(c => c.ContractId).Distinct().Count());
            Assert.All(allPages, c => Assert.Equal("ES", c.Symbol));

            // Retained page tokens permit replay of an already visited page.
            var replay = await db.GetFuturesOptionContractsPageAsync(new("ES", 200, first.ContinuationToken), token);
            Assert.Equal(second.Items.Select(c => c.ContractId), replay.Items.Select(c => c.ContractId));
            await Assert.ThrowsAsync<ArgumentException>(() => db.GetFuturesOptionContractsPageAsync(new("NQ", 200, first.ContinuationToken), token));
            await Assert.ThrowsAsync<ArgumentException>(() => db.GetFuturesOptionContractsPageAsync(new("ES", 100, first.ContinuationToken), token));
            await Assert.ThrowsAsync<ArgumentException>(() => db.GetFuturesOptionContractsPageAsync(new("ES", 200, "tampered"), token));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.GetFuturesOptionContractsPageAsync(new("ES", 0), token));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.GetFuturesOptionContractsPageAsync(new("ES"), canceled.Token));

            await db.InsertFuturesOptionContractAsync(Contract("ES", 999));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.GetFuturesOptionContractsPageAsync(new("ES", 200, first.ContinuationToken), token));
            var refreshed = await db.GetFuturesOptionContractsPageAsync(new("ES"), token);
            Assert.Equal(200, refreshed.Items.Length);
            Assert.NotEqual(first.ContinuationToken, refreshed.ContinuationToken);

            // Unready projections fail explicitly; the paged read must never repair by scanning.
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.GetFuturesOptionContractsPageAsync(new("UNREADY"), token));
            await db.BackfillSymbolProjectionsAsync(cancellationToken: token);
            var empty = await db.GetFuturesOptionContractsPageAsync(new("EMPTY"), token);
            Assert.Empty(empty.Items);
            Assert.False(empty.HasMore);
        }
        finally
        {
            // Only the unique keyspace created by this test is removed.
            await admin.Use("OptionPages.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }

    static FuturesOptionContractReadModel Contract(string symbol, int index) => new(
        contractId: $"{symbol}20260918C{4000 + index}", symbol: symbol, localSymbol: $"{symbol}{index}",
        securityType: "FOP", currency: "USD", exchange: "CME", multiplier: "50",
        contractMonth: new DateOnly(2026, 9, 18), optionType: "Call", strikePrice: 4000 + index,
        description: "Isolated paging integration test");

    sealed class Admin(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Admin>(setting, logger)
    { public override IObjectRepository Database => this; }
}
