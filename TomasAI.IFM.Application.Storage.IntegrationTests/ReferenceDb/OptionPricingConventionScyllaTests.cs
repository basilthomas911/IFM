using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using Xunit;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

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
            var banded = value with { SchemaVersion = 2, MappingVersion = "fixture/v3", TickSize = .05m,
                PremiumTickRule = OptionPremiumTickRule.CmeEsGlobex358A, TickRuleVersion = OptionPremiumTicks.CmeEsGlobexVersion };
            await restarted.InsertReviewedAsync(banded, token);
            await restarted.InsertReviewedAsync(banded, token);
            var reloaded = await new OptionPricingConventionStore(new Repository(settings["test"], logger))
                .GetAsync(banded.ContractId, banded.MappingVersion, token);
            Assert.Equal(banded, reloaded);
            Assert.Equal(.10m, OptionPremiumTicks.GetIncrement(reloaded!, 15m));
            await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.InsertReviewedAsync(banded with
                { PremiumTickRule = OptionPremiumTickRule.Fixed }, token));
            await db.Use("OcpTest.BundleSchema", OptionPricingReferenceBundleStore.CreateTable).ExecuteCommandAsync(token);
            var bundles = new OptionPricingReferenceBundleStore(db);
            var expiry = DateOnly.FromDateTime(value.ExpirationUtc.UtcDateTime);
            var candidate = new OptionDefinitionCandidate(value.ContractId, value.MappingVersion, value.DefinitionDigest,
                new OptionContractDefinition { Dataset = value.Dataset, RawSymbol = value.RawSymbol, Ticker = "ES",
                    Underlying = value.UnderlyingContractId, Instrument = new(value.PublisherId, value.InstrumentId),
                    Right = OptionRightSelection.Call, StrikePrice = 5000, MaturityDate = expiry,
                    ExpirationTimestampNanoseconds = checked((ulong)(value.ExpirationUtc - DateTimeOffset.UnixEpoch).Ticks * 100) });
            var bundle = new OptionPricingReferenceBundle(1, "", value.MappingVersion, expiry,
                new() { Dataset = value.Dataset, DomainContractId = value.UnderlyingContractId, ProviderContractName = "fixture-future",
                    RootSymbol = "ES", AssetTypeId = AssetTypeId.Futures },
                new(value.CalendarVersion, "America/New_York", expiry.AddDays(-1), expiry, new(18, 0), [expiry.AddDays(-1), expiry]),
                UsTreasuryPublicationCalendar.Default2026, UsTreasuryCurve.ConversionPolicy, [candidate]).Seal();
            await bundles.PublishAsync(bundle, token);
            await bundles.PublishAsync(bundle, token);
            var restoredBundle = await new OptionPricingReferenceBundleStore(new Repository(settings["test"], logger)).ReadAsync(bundle.BundleId, token);
            Assert.Equal(bundle.BundleId, restoredBundle!.BundleId);
            Assert.Equal(candidate, Assert.Single(restoredBundle.Definitions));
            Assert.Single(restoredBundle.CreatePlan(4999, 5001).Options);
            await Assert.ThrowsAsync<InvalidDataException>(() => bundles.PublishAsync((bundle with
                { Definitions = [candidate with { DefinitionDigest = new('b', 64) }] }).Seal(), token));
            await Assert.ThrowsAsync<InvalidDataException>(() => bundles.PublishAsync(bundle with { ProfileVersion = "tampered" }, token));
            Assert.Throws<InvalidDataException>(() => restoredBundle.CreatePlan(5001, 5010));
        }
        finally
        {
            await admin.Use("OcpTest.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }
    }
    sealed class Repository(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Repository>(setting, logger)
    { public override IObjectRepository Database => this; }
}
