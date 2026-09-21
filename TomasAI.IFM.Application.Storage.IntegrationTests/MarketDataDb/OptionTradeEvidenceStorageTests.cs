using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class OptionTradeEvidenceStorageTests
{
    [Fact]
    public async Task Failed_pricing_source_is_retained_once_across_retry_restart_and_conflicting_duplicate()
    {
        var keyspace = "ifm_trade_" + Guid.NewGuid().ToString("N");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("test", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Db(settings["admin"], logger);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = deadline.Token;
        await admin.Use("TradeEvidence.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            var db = new Db(settings["test"], logger);
            await db.Use("TradeEvidence.Schema", OptionTradeEvidenceStore.CreateTable).ExecuteCommandAsync(token);
            await db.Use("TradeEvidence.Schema", OptionTradeEvidenceStore.CreateTable).ExecuteCommandAsync(token);
            var store = new OptionTradeEvidenceStore(db);
            var source = new OptionTradeSource("GLBX.MDP3", 1, 42, "ES20261218C6500.5", new(2026, 9, 19),
                12.5m, 2, 99, 1789812345678900123, 1789812345678910456, Guid.NewGuid(), "fixture");
            var original = new OptionTradeEvidence(source, null, null, DateTimeOffset.UtcNow, null,
                new("PricingContextUnavailable", "Context", source.ContractId, "Synthetic missing-rate fixture."));
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.WriteAsync(original, token).AsTask()));
            var restarted = new OptionTradeEvidenceStore(new Db(settings["test"], logger));
            var retained = await restarted.ReadAsync(source.ContractId, source.ValueDate, source.Identity, token);
            Assert.Equal(original, retained);
            var retry = original with { Source = source with { GenerationId = Guid.NewGuid(), ReceiveNanoseconds = source.ReceiveNanoseconds + 100 },
                CalculatedAtUtc = original.CalculatedAtUtc.AddMinutes(1), Failure = original.Failure! with { Code = "DifferentRetryFailure" } };
            await restarted.WriteAsync(retry, token);
            Assert.Equal(original, await restarted.ReadAsync(source.ContractId, source.ValueDate, source.Identity, token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.WriteAsync(original with
                { Source = source with { Price = 13 } }, token).AsTask());
            Assert.Equal(original, await restarted.ReadAsync(source.ContractId, source.ValueDate, source.Identity, token));
            var count = await db.Use("TradeEvidence.Count", "SELECT count(*) FROM option_trade_evidence;")
                .ExecuteQueryAsync(row => row.GetLong(0), token);
            Assert.Equal(1, Assert.Single(count));
        }
        finally
        {
            await admin.Use("TradeEvidence.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }
    sealed class Db(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Db>(setting, logger)
    { public override IObjectRepository Database => this; }
}
