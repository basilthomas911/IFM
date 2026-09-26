using System;
using System.Linq;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
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

public sealed class FuturesReferenceMetadataStorageTests
{
    [Fact]
    public async Task Additive_migration_preserves_legacy_and_exact_decimal_metadata_across_restart_and_pages()
    {
        var keyspace = "ifm_ref_" + Guid.NewGuid().ToString("N");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add(SecuritiesDbContext.SecuritiesDbConnection, $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new Admin(settings["admin"], logger);
        await admin.Use("ReferenceMetadata.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};").ExecuteCommandAsync(token);
        try
        {
            var schema = new SecuritiesSchemaDb(settings, logger);
            // Install the unchanged legacy tables first; add data before the new columns exist.
            await schema.CreateAsync(["futures_contract_v3", "futures_option_contract"], token);
            var raw = new Admin(settings[SecuritiesDbContext.SecuritiesDbConnection], logger);
            await raw.Use("ReferenceMetadata.Legacy", """
                INSERT INTO futures_option_contract(contractId,description,symbol,localSymbol,securityType,currency,exchange,multiplier,contractMonth,strikePrice,optionType)
                VALUES('ES20260918C6500','legacy','ES','legacy','FOP','USD','CME','50','2026-09-18',6500,'Call');
                """).ExecuteCommandAsync(token);
            await schema.CreateAllAsync();
            await schema.CreateAllAsync(); // exact additive DDL is restart/idempotence safe
            var db = Open();
            var legacy = await db.GetFuturesOptionContractAsync("ES20260918C6500", token);
            Assert.NotNull(legacy);
            Assert.Null(legacy!.StrikePriceDecimal);
            Assert.Equal(6500m, legacy.GetExactStrikePrice());
            Assert.Equal(ReferenceReviewState.Unknown, legacy.ReviewState);
            Assert.Equal(ReferenceExerciseStyle.Unknown, legacy.ExerciseStyle);

            const decimal strike = 6500.1234567890123456789012345m;
            var future = new FuturesContractV3ReadModel("ES20260918", "fixture", "ES", "ESU6", "FUT", "USD", "CME", "50",
                new(2026, 9, 18), false)
            {
                SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft, Dataset = "GLBX.MDP3",
                PublisherId = 1, InstrumentId = 99, RawSymbol = "ESU6", MultiplierValue = 50,
                PriceScale = 1, TickSize = .25m, DefinitionDigest = new('a', 64), MappingVersion = "fixture/v1"
            };
            var option = new FuturesOptionContractReadModel("ES20260918C6500.1234567890123456789012345",
                "fixture", "ES", "fractional", "FOP", "USD", "CME", "50", new(2026, 9, 18), (double)strike, "Call")
            {
                SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft, StrikePriceDecimal = strike,
                Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = 42, RawSymbol = "fractional",
                UnderlyingContractId = future.ContractId, UnderlyingAssetType = ReferenceAssetType.Futures,
                UnderlyingInstrumentId = 99, UnderlyingPublisherId = 1,
                OptionRight = ReferenceOptionRight.Call, ExerciseStyle = ReferenceExerciseStyle.American,
                PremiumStyle = ReferencePremiumStyle.PremiumPaid, DefinitionDigest = new('b', 64), MappingVersion = "fixture/v1"
            };
            await db.InsertFuturesContractAsync(future);
            await db.InsertFuturesOptionContractAsync(option);
            var restarted = Open();
            Assert.Equal(future, await restarted.GetFuturesContractAsync(future.ContractId, token));
            Assert.Equal(option, await restarted.GetFuturesOptionContractAsync(option.ContractId, token));
            await restarted.BackfillSymbolProjectionsAsync(cancellationToken: token);
            var page = await restarted.GetFuturesOptionContractsPageAsync(new("ES"), token);
            Assert.Equal(2, page.Items.Length);
            Assert.Equal(option, Assert.Single(page.Items, x => x.ContractId == option.ContractId));
            Assert.Equal(strike, Assert.Single(page.Items, x => x.ContractId == option.ContractId).GetExactStrikePrice());
            // Legacy column readers still see the old representation and both source rows remain.
            var counts = await raw.Use("ReferenceMetadata.Count", "SELECT count(*) FROM futures_option_contract;")
                .ExecuteQueryAsync(row => row.GetLong(0), token);
            Assert.Equal(2L, Assert.Single(counts));

            // A reviewed version is the single durable source for reference and pricing convention.
            var start = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
            var expiry = new DateTimeOffset(2026, 9, 18, 20, 0, 0, TimeSpan.Zero);
            var reviewedFuture = future with
            {
                ReviewState = ReferenceReviewState.Reviewed, DefinitionTimestampUtc = start,
                RawDefinitionReference = "fixture/future", ExpirationUtc = expiry, LastTradingUtc = expiry,
                ExchangeTimeZoneId = "America/New_York", CalendarVersion = "fixture/calendar",
                SettlementStyle = ReferenceSettlementStyle.Cash, EvidenceId = "fixture/review",
                EffectiveFromUtc = start, EffectiveUntilUtc = expiry
            };
            await db.UpdateFuturesContractAsync(future.Id, reviewedFuture);
            var reviewedOption = option with
            {
                ReviewState = ReferenceReviewState.Reviewed, DefinitionTimestampUtc = start,
                RawDefinitionReference = "fixture/option", ExpirationUtc = expiry, LastTradingUtc = expiry,
                ExchangeTimeZoneId = "America/New_York", CalendarVersion = "fixture/calendar",
                SettlementStyle = ReferenceSettlementStyle.DeliveryOfFuture, EvidenceId = "fixture/review",
                EffectiveFromUtc = start, EffectiveUntilUtc = expiry, MultiplierValue = 50, PriceScale = 1,
                TickSize = .25m, PremiumTickRule = ReferencePremiumTickRule.Fixed, TickRuleVersion = "fixture/ticks",
                DayCount = ReferenceDayCount.Actual365Fixed
            };
            await db.UpdateFuturesOptionContractAsync(option.ContractId, reviewedOption);
            var versions = new ReferenceVersionStore(Open());
            var first = await versions.GetAsync(option.ContractId, "fixture/v1", token);
            Assert.Equal(reviewedOption, first!.Option);
            Assert.Equal(strike, first.Convention!.Strike);
            Assert.Equal(TomasAI.IFM.Framework.MarketData.Contracts.Pricing.OptionExerciseStyle.American, first.Convention.ExerciseStyle);
            // Given an imported American reference, retain a real full-Greek Trade-basis result and reopen it.
            var at = new DateTimeOffset(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
            var rate = TreasuryRateConversion.Convert(new TreasuryCurveSnapshot(new(2026, 9, 8),
                [new(TreasuryTenor.OneMonth, 5m)], at, "FinancialModelingPrep"), TreasuryTenor.OneMonth,
                new("FinancialModelingPrep", "fixture:CMT", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual,
                    "test/v1", "synthetic-evidence")).Value!;
            var calendar = new OptionPricingCalendar("fixture/calendar", "America/New_York",
                new(2026, 1, 1), new(2026, 12, 31), new(18, 0),
                Enumerable.Range(0, 365).Select(i => new DateOnly(2026, 1, 1).AddDays(i))
                    .Where(x => x.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToImmutableArray());
            var context = new OptionPricingContext(first.Convention, calendar, rate, at.AddHours(1), Guid.NewGuid(),
                Black76PricingModel.EngineFor(first.Convention), 1000, 250, "fixture/publication");
            var underlyingQuote = new OptionPricingQuote(future.ContractId, 6500m, 6500.5m, 1, 1, at, at, 1, context.GenerationId);
            var trade = new LastTradeTickSnapshot(option.ContractId, new(2026, 9, 8), 12.5m, 1, 2, at, at);
            var priced = OptionTradePricing.Calculate(context, underlyingQuote, trade, strike, true, at);
            Assert.True(priced.IsValid, priced.PricingFailure?.Code);
            var nanos = (at.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100 + 23;
            var evidence = new OptionTradeEvidence(new("GLBX.MDP3", 1, 42, option.ContractId, trade.ValueDate,
                trade.Price, trade.Size, trade.SourceSequence, nanos, nanos + 123, context.GenerationId, option.RawSymbol!),
                context, underlyingQuote, at, new(priced.ImpliedVolatility!.Value, priced.Delta!.Value, priced.Gamma!.Value,
                    priced.Theta!.Value, priced.Vega!.Value, priced.Rho!.Value, priced.TheoreticalPrice!.Value,
                    priced.TimeToExpiryYears!.Value, priced.PricingContextDigest!), null);
            var marketDataSettings = new DbConnectionSettings().Add(
                MarketDataDbContext.MarketDataDbConnection,
                settings[SecuritiesDbContext.SecuritiesDbConnection].ConnectionString,
                settings[SecuritiesDbContext.SecuritiesDbConnection].ProviderName);
            await new MarketDataSchemaDb(marketDataSettings, logger)
                .CreateAsync(["option_trade_evidence"], token);
            var evidenceStore = MarketDataDbContextTestFactory.Create(
                settings[SecuritiesDbContext.SecuritiesDbConnection]);
            await evidenceStore.WriteAsync(evidence, token);
            var tradeCopy = await MarketDataDbContextTestFactory.Create(
                    settings[SecuritiesDbContext.SecuritiesDbConnection])
                .ReadAsync(option.ContractId, trade.ValueDate, evidence.Source.Identity, token);
            Assert.Equal(evidence.Greeks, tradeCopy!.Greeks);
            Assert.Equal(nanos, tradeCopy.Source.EventNanoseconds);
            Assert.Equal(context.Contract, tradeCopy.Context!.Contract);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.InsertFuturesOptionContractAsync(
                reviewedOption with { InstrumentId = 123, MappingVersion = "collision/v1" }));
            Assert.Equal(reviewedOption, await db.GetFuturesOptionContractAsync(option.ContractId, token));
            var second = reviewedOption with { MappingVersion = "fixture/v2", EvidenceId = "fixture/correction" };
            var staged = await versions.StageAsync(second, token);
            Assert.Null(await versions.GetAsync(option.ContractId, "fixture/v2", token));
            await db.UpdateFuturesOptionContractAsync(option.ContractId, second);
            Assert.Equal(second, (await versions.GetAsync(option.ContractId, "fixture/v2", token))!.Option);
            Assert.Equal(reviewedOption, (await versions.GetAsync(option.ContractId, "fixture/v1", token))!.Option);
            Assert.Equal(reviewedOption, (await versions.GetEffectiveAsync(option.ContractId, "fixture/v1", start, token))!.Option);
            Assert.Null(await versions.GetEffectiveAsync(option.ContractId, "fixture/v1", expiry, token));
            Assert.Contains(await versions.ListVersionsAsync(option.ContractId, token: token),
                row => row.Version == "fixture/v1" && row.Published);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpdateFuturesOptionContractAsync(
                option.ContractId, second with { MappingVersion = "wrong-underlying", UnderlyingInstrumentId = 999 }));
            var concurrent = reviewedFuture with { ContractId = "ES20260919", LastTradeDate = new(2026, 9, 19),
                InstrumentId = 999, MappingVersion = "concurrent/v1" };
            async Task<bool> TryClaim(uint instrument)
            {
                try { await versions.StageAsync(concurrent with { InstrumentId = instrument }, token); return true; }
                catch (InvalidOperationException) { return false; }
            }
            var claims = await Task.WhenAll(TryClaim(999), TryClaim(1000));
            Assert.Single(claims, x => x);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpdateFuturesOptionContractAsync(
                option.ContractId, second with { EvidenceId = "conflicting-content" }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpdateFuturesOptionContractAsync(
                option.ContractId, option));
            // Crash boundary: canonical future exists, but its immutable version is not yet published.
            await raw.Use("ReferenceMetadata.InterruptedPublication",
                "UPDATE securities_reference_version SET published=false WHERE contract_id='ES20260918' AND version='fixture/v1';")
                .ExecuteCommandAsync(token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.UpdateFuturesOptionContractAsync(
                option.ContractId, second with { MappingVersion = "fixture/v3" }));
            var repair = await versions.StageAsync(reviewedFuture, token);
            await versions.CommitAsync(repair, token);
            await db.UpdateFuturesOptionContractAsync(option.ContractId, second with { MappingVersion = "fixture/v3" });
            Assert.NotNull(await versions.GetAsync(option.ContractId, "fixture/v3", token));
        }
        finally
        {
            // Only the unique test-owned keyspace is removed.
            await admin.Use("ReferenceMetadata.Drop", $"DROP KEYSPACE IF EXISTS {keyspace};").ExecuteCommandAsync(CancellationToken.None);
        }

        SecuritiesDbContext Open()
        {
            var factory = Substitute.For<IDbContextFactory>();
            var db = new SecuritiesDbContext(settings, factory, logger);
            factory.SecuritiesDb.Returns(db);
            return db;
        }
    }

    sealed class Admin(IDbConnectionSetting setting, ILogger<DbProvider> logger) : ObjectDataRepository<Admin>(setting, logger)
    { public override IObjectRepository Database => this; }
}
