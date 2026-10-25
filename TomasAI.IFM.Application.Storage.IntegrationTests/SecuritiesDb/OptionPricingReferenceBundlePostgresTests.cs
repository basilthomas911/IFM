using TomasAI.IFM.Shared.MarketData.Definitions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Securities.OptionPricing.Repository;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

public sealed class OptionPricingReferenceBundlePostgresTests
{
    [Fact]
    public async Task Exact_reviewed_version_roundtrips_restarts_and_refuses_conflicting_content()
    {
        await using var infrastructure = new TomasAI.IFM.IntegrationTesting.IsolatedIntegrationInfrastructure("pricingbundlerepository");
        await infrastructure.StartAsync();
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var token = timeout.Token;
        await new SecuritiesSchemaDb(settings, logger).CreateAsync(["securities"], token);
        {
            var db = Open();
            var store = db;
            var at = new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
            var value = new OptionPricingConvention
            {
                ContractId = "ES20260909C5000",
                Dataset = "GLBX.MDP3",
                PublisherId = 1,
                InstrumentId = 123,
                RawSymbol = "fixture-only",
                Root = "ES",
                Exchange = "XCME",
                Currency = "USD",
                UnderlyingContractId = "ES20261218",
                ExerciseStyle = OptionExerciseStyle.European,
                SettlementStyle = OptionSettlementStyle.DeliveryOfFuture,
                ExpirationUtc = at.AddDays(1),
                LastTradingUtc = at.AddDays(1),
                DayCount = PricingDayCount.Actual365Fixed,
                CalendarVersion = "fixture/v1",
                Multiplier = 50,
                TickSize = .25m,
                TickRuleVersion = "fixture/v1",
                DefinitionDigest = new string('a', 64),
                MappingVersion = "fixture/v1",
                EvidenceId = "synthetic-only",
                EffectiveFromUtc = at.AddDays(-1),
                EffectiveUntilUtc = at.AddDays(2)
            };
            Assert.Null(await store.GetAsync(value.ContractId, value.MappingVersion, token));
            await PublishAsync(store, value, token);
            await PublishAsync(store, value, token);
            var restarted = Open();
            Assert.Equal(ReviewedOptionConvention.From(Definition(value)), await restarted.GetAsync(value.ContractId, value.MappingVersion, token));
            Assert.Null(await restarted.GetAsync(value.ContractId, "missing-version", token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => PublishAsync(restarted, value with { Multiplier = 100 }, token));
            Assert.Equal(ReviewedOptionConvention.From(Definition(value)), await restarted.GetAsync(value.ContractId, value.MappingVersion, token));
            var newVersion = value with { MappingVersion = "fixture/v2", Multiplier = 100 };
            await PublishAsync(restarted, newVersion, token);
            Assert.Equal(ReviewedOptionConvention.From(Definition(newVersion)), await restarted.GetAsync(value.ContractId, newVersion.MappingVersion, token));
            var banded = value with
            {
                SchemaVersion = 2,
                MappingVersion = "fixture/v3",
                TickSize = .05m,
                PremiumTickRule = OptionPremiumTickRule.CmeEsGlobex358A,
                TickRuleVersion = OptionPremiumTicks.CmeEsGlobexVersion
            };
            await PublishAsync(restarted, banded, token);
            await PublishAsync(restarted, banded, token);
            var reloaded = await Open()
                .GetAsync(banded.ContractId, banded.MappingVersion, token);
            Assert.Equal(ReviewedOptionConvention.From(Definition(banded)), reloaded);
            Assert.Equal(.10m, OptionPremiumTicks.GetIncrement(reloaded!, 15m));
            await Assert.ThrowsAsync<InvalidOperationException>(() => PublishAsync(restarted, banded with
            { PremiumTickRule = OptionPremiumTickRule.Fixed }, token));
            
            var bundles = new OptionPricingReferenceBundleRepository(db);
            var expiry = DateOnly.FromDateTime(value.ExpirationUtc.UtcDateTime);
            var candidate = new OptionDefinitionCandidate(value.ContractId, value.MappingVersion, value.DefinitionDigest,
                new OptionContractDefinition
                {
                    Dataset = value.Dataset,
                    RawSymbol = value.RawSymbol,
                    Ticker = "ES",
                    Underlying = value.UnderlyingContractId,
                    Instrument = new(value.PublisherId, value.InstrumentId),
                    Right = OptionRightSelection.Call,
                    StrikePrice = 5000,
                    MaturityDate = expiry,
                    ExpirationTimestampNanoseconds = checked((ulong)(value.ExpirationUtc - DateTimeOffset.UnixEpoch).Ticks * 100)
                });
            var bundle = new OptionPricingReferenceBundle(1, "", value.MappingVersion, expiry,
                new()
                {
                    Dataset = value.Dataset,
                    DomainContractId = value.UnderlyingContractId,
                    ProviderContractName = "ES20261218",
                    RootSymbol = "ES",
                    AssetTypeId = AssetTypeId.Futures
                },
                new(value.CalendarVersion, "America/New_York", expiry.AddDays(-1), expiry, new(18, 0), [expiry.AddDays(-1), expiry]),
                UsTreasuryPublicationCalendar.Default2026, UsTreasuryCurve.ConversionPolicy, [candidate]).Seal();
            var unpublished = (bundle with
            { Definitions = [candidate with { MappingVersion = "unpublished/v1" }] }).Seal();
            await Assert.ThrowsAsync<InvalidDataException>(() => bundles.PublishAsync(unpublished, token));
            Assert.Null(await db.GetOptionPricingReferenceBundlePayloadAsync(unpublished.BundleId, token));
            var exact = ReviewedOptionConvention.From(Definition(value));
            Assert.Throws<InvalidDataException>(() => ReviewedOptionReference.Apply(Definition(value), exact with { Strike = 5001 }));
            Assert.Throws<InvalidDataException>(() => ReviewedOptionReference.Apply(Definition(value), exact with { InstrumentId = 777 }));
            await bundles.PublishAsync(bundle, token);
            await bundles.PublishAsync(bundle, token);
            var restoredBundle = await new OptionPricingReferenceBundleRepository(Open()).ReadAsync(bundle.BundleId, token);
            Assert.Equal(bundle.BundleId, restoredBundle!.BundleId);
            Assert.Equal(candidate, Assert.Single(restoredBundle.Definitions));
            Assert.Single(restoredBundle.CreatePlan(4999, 5001).Options);
            await Assert.ThrowsAsync<InvalidDataException>(() => bundles.PublishAsync((bundle with
            { Definitions = [candidate with { DefinitionDigest = new('b', 64) }] }).Seal(), token));
            await Assert.ThrowsAsync<InvalidDataException>(() => bundles.PublishAsync(bundle with { ProfileVersion = "tampered" }, token));
            Assert.Throws<InvalidDataException>(() => restoredBundle.CreatePlan(5001, 5010));
        }
        SecuritiesDbContext Open()
        {
            var factory = Substitute.For<IDbContextFactory>();
            var db = new SecuritiesDbContext(settings, factory, logger);
            factory.SecuritiesDb.Returns(db);
            return db;
        }
    }
    static FuturesOptionContractReadModel Definition(OptionPricingConvention value)
    {
        var source = new FuturesOptionContractReadModel(value.ContractId, "fixture", "ES", value.RawSymbol,
            "FOP", value.Currency, value.Exchange, value.Multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.FromDateTime(value.ExpirationUtc.UtcDateTime), 5000, "Call")
        {
            Dataset = value.Dataset, PublisherId = value.PublisherId, InstrumentId = value.InstrumentId,
            RawSymbol = value.RawSymbol, DefinitionTimestampUtc = value.EffectiveFromUtc,
            DefinitionDigest = value.DefinitionDigest, RawDefinitionReference = "fixture:" + value.DefinitionDigest,
            ExchangeTimeZoneId = "America/New_York", PriceScale = 1, StrikePriceDecimal = 5000,
            UnderlyingContractId = value.UnderlyingContractId, UnderlyingInstrumentId = 99, UnderlyingPublisherId = 1,
            UnderlyingAssetType = ReferenceAssetType.Futures,
            PremiumStyle = ReferencePremiumStyle.PremiumPaid, OptionRight = ReferenceOptionRight.Call
        };
        return ReviewedOptionReference.Apply(source, value);
    }

    static async Task PublishAsync(SecuritiesDbContext db, OptionPricingConvention value, CancellationToken token)
    {
        var pending = await db.StageReferenceVersionAsync(Definition(value), token);
        await db.CommitReferenceVersionAsync(pending, token);
    }

}
