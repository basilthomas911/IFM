using System;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class OptionPricingConventionScyllaTests
{
    [Fact]
    public async Task Exact_reviewed_version_roundtrips_restarts_and_refuses_conflicting_content()
    {
        // Only this randomly generated keyspace is created/dropped, on the local test server.
        var keyspace = "ifm_ocp_" + Guid.NewGuid().ToString("N");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("test", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Repository(settings["admin"], logger);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        await admin.Use("OcpTest.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            var db = new Repository(settings["test"], logger);
            await db.Use("OcpTest.Schema", OptionPricingConventionStore.CreateTable).ExecuteCommandAsync(token);
            var store = new OptionPricingConventionStore(db);
            var at = new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
            var value = new OptionPricingConvention
            {
                ContractId = "fixture-option", Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = 123,
                RawSymbol = "fixture-only", Root = "ES", Exchange = "XCME", Currency = "USD", UnderlyingContractId = "fixture-future",
                ExerciseStyle = OptionExerciseStyle.European, SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
                ExpirationUtc = at.AddDays(1), LastTradingUtc = at.AddDays(1), DayCount = PricingDayCount.Actual365Fixed,
                CalendarVersion = "fixture/v1", Multiplier = 50, TickSize = .25m, TickRuleVersion = "fixture/v1",
                DefinitionDigest = new string('a', 64), MappingVersion = "fixture/v1", EvidenceId = "synthetic-only",
                EffectiveFromUtc = at.AddDays(-1), EffectiveUntilUtc = at.AddDays(2)
            };
            Assert.Null(await store.GetAsync(value.ContractId, value.MappingVersion, token));
            await store.InsertReviewedAsync(value, token);
            await store.InsertReviewedAsync(value, token);
            var restarted = new OptionPricingConventionStore(new Repository(settings["test"], logger));
            Assert.Equal(value, await restarted.GetAsync(value.ContractId, value.MappingVersion, token));
            Assert.Null(await restarted.GetAsync(value.ContractId, "missing-version", token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.InsertReviewedAsync(value with { Multiplier = 100 }, token));
            Assert.Equal(value, await restarted.GetAsync(value.ContractId, value.MappingVersion, token));
            var newVersion = value with { MappingVersion = "fixture/v2", Multiplier = 100 };
            await restarted.InsertReviewedAsync(newVersion, token);
            Assert.Equal(newVersion, await restarted.GetAsync(value.ContractId, newVersion.MappingVersion, token));
        }
        finally
        {
            await admin.Use("OcpTest.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }
    sealed class Repository(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Repository>(setting, logger)
    { public override IObjectRepository Database => this; }
}
