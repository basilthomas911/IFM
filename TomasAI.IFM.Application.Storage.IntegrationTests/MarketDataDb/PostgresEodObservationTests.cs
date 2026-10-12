using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using System.Threading;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Tests native direct-observation transactions without event-log or Scylla schema dependencies.</summary>
public sealed class PostgresEodObservationTests
{
    static async Task VerifyCanonicalQueryMaintenanceAsync(Storage.MarketDataDb.MarketDataDbContext db, string connectionString)
    {
        var signal = SampleData.FuturesItiSignal1 with { ContractId = "MAINTENANCE-ITI", ValueDate = new DateOnly(2026,10,9), TimePeriod = TimeFrameType.Daily, SequenceId = 900, IntrinsicTime = new DateTime(2026,10,9,14,0,0,DateTimeKind.Utc) };
        await db.InsertFuturesItiSignalAsync(signal);
        var ready = await db.GetQueryProjectionReadinessAsync();
        Assert.True(ready.IsReady);
        Assert.True(ready.RawTicksManagedByTradeTickQuoteDb);
        Assert.False(ready.FuturesTickByTime);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var drop = new NpgsqlCommand("DROP INDEX market_data.futures_iti_signal_ordered;", connection))
            await drop.ExecuteNonQueryAsync();
        Assert.False((await db.GetQueryProjectionReadinessAsync()).FuturesItiSignalQueries);
        await using (var wrong = new NpgsqlCommand("CREATE INDEX futures_iti_signal_ordered ON market_data.futures_iti_signal(contractId);", connection))
            await wrong.ExecuteNonQueryAsync();
        Assert.False((await db.GetQueryProjectionReadinessAsync()).FuturesItiSignalQueries);
        await using (var dropWrong = new NpgsqlCommand("DROP INDEX market_data.futures_iti_signal_ordered;", connection))
            await dropWrong.ExecuteNonQueryAsync();
        var owner = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, connectionString, "System.Data.Postgres"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteAsync<int>(async (transaction, token) =>
        {
            var result = await transaction.RebuildCanonicalQueryIndexesAsync(token);
            Assert.True(result.CutoverCompleted);
            throw new InvalidOperationException("rollback probe");
        }, CancellationToken.None));
        Assert.False((await db.GetQueryProjectionReadinessAsync()).FuturesItiSignalQueries);
        var repaired = await db.BackfillQueryProjectionsV2Async(batchSize: 1);
        Assert.True(repaired.IsReconciled);
        Assert.True(repaired.CutoverCompleted);
        Assert.True(repaired.RawTicksManagedByTradeTickQuoteDb);
        Assert.True(repaired.FuturesItiSignalsSource > 0);
        Assert.Equal(0, repaired.FuturesTicksSource);
        Assert.True((await db.GetQueryProjectionReadinessAsync()).IsReady);
        var repeated = await db.BackfillQueryProjectionsV2Async();
        Assert.Equal(repaired, repeated);
        await db.InsertFuturesEodDataAsync(new[] { SampleData.FuturesEodData with
            { ContractId = "MAINTENANCE", ValueDate = new DateOnly(2026, 10, 9), ClosePrice = 6000m } });
        Assert.True((await db.GetQueryProjectionReadinessAsync()).IsReady);
        var updated = await db.BackfillQueryProjectionsV2Async();
        Assert.True(updated.FuturesEodRowsSource > repaired.FuturesEodRowsSource);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => db.BackfillQueryProjectionsV2Async(0));
        await Assert.ThrowsAsync<ArgumentException>(() => db.BackfillQueryProjectionsV2Async(staleOperationCutoffUtc: DateTime.Now));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.GetQueryProjectionReadinessAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.BackfillQueryProjectionsV2Async(cancellationToken: cancelled.Token));
        await db.DeleteFuturesItiSignalAsync(signal.ContractId, signal.ValueDate, signal.TimePeriod);
        await db.DeleteFuturesEodDataAsync("MAINTENANCE", new DateOnly(2026,10,9));
    }

    static async Task VerifyTradeSignalsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date, string connectionString)
    {
        var first = SampleData.FuturesTradeSignal with { ContractId = "ES-SIGNAL", ValueDate = date, TimePeriod = TimeFrameType.FifteenSeconds, SequenceId = 700, Timestamp = new TimeOnly(14,0), MDI = 0.125 };
        var latest = first with { SequenceId = 701, Timestamp = new TimeOnly(14,1), FuturesPrice = 6001.125 };
        await db.InsertFuturesTradeSignalAsync(first);
        await db.InsertFuturesTradeSignalAsync(latest);
        await db.InsertFuturesTradeSignalAsync(first);
        (await db.GetLastFuturesTradeSignalAsync(first.ContractId,date)).Should().BeEquivalentTo(latest);
        (await db.GetLastFuturesTradeSignalAsync()).Should().BeEquivalentTo(latest);
        Assert.Equal(2,(await db.GetFuturesTradeSignalsAsync()).Count);
        Assert.Equal(701,(await db.GetFuturesTradeSignalIdByValueDateAsync(date)).Single().SequenceId);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("ALTER TABLE market_data.futures_trade_signal_lookup_by_scope ADD CONSTRAINT signal_rollback_probe CHECK (sequenceId <> 702)",connection))
            await command.ExecuteNonQueryAsync();
        Func<Task> fail = () => db.InsertFuturesTradeSignalAsync(latest with { SequenceId = 702, Timestamp = new TimeOnly(14,2) });
        await fail.Should().ThrowAsync<Exception>();
        Assert.Equal(2,(await db.GetFuturesTradeSignalsAsync()).Count);
        (await db.GetLastFuturesTradeSignalAsync()).Should().BeEquivalentTo(latest);
        await using (var corrupt = new NpgsqlCommand("UPDATE market_data.futures_trade_signal_lookup_by_scope SET sequenceId=999999;",connection))
            await corrupt.ExecuteNonQueryAsync();
        var repaired = await db.RepairFuturesTradeSignalLookupAsync();
        Assert.Equal(2, repaired.ValidRows);
        (await db.GetLastFuturesTradeSignalAsync()).Should().BeEquivalentTo(latest);
        Assert.Equal(2,(await db.GetFuturesTradeSignalsAsync()).Count);
        await using (var constraint = new NpgsqlCommand("ALTER TABLE market_data.futures_trade_signal_lookup_by_scope ADD CONSTRAINT repair_rollback_probe CHECK (sequenceId <> 701) NOT VALID;",connection))
            await constraint.ExecuteNonQueryAsync();
        try
        {
            Func<Task> failedRepair = () => db.RepairFuturesTradeSignalLookupAsync();
            await failedRepair.Should().ThrowAsync<PostgresException>();
            // The delete and replacements share a transaction: failed reconstruction preserves existing lookups.
            (await db.GetLastFuturesTradeSignalAsync()).Should().BeEquivalentTo(latest);
            Assert.Equal(2,(await db.GetFuturesTradeSignalsAsync()).Count);
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("ALTER TABLE market_data.futures_trade_signal_lookup_by_scope DROP CONSTRAINT repair_rollback_probe;",connection);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    static async Task VerifyItiSignalsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date, string connectionString)
    {
        var first = SampleData.FuturesItiSignal1 with { ContractId = "ES-ITI", ValueDate = date, TimePeriod = TimeFrameType.Daily, SequenceId = 800, IntrinsicTime = new DateTime(2026,10,7,14,0,0,DateTimeKind.Utc) };
        var latest = first with { SequenceId = 801, IntrinsicPrice = 6001.25, BandAnchorPrice = 6000, BandPercentage = 0.15, BandSize = 1.5 };
        await db.InsertFuturesItiSignalAsync(first);
        await db.InsertFuturesItiSignalAsync(latest);
        await db.InsertFuturesItiSignalAsync(first);
        var rows = await db.GetFuturesItiSignalsForContractAsync(first.ContractId,date,date);
        Assert.Equal(new long[] {800,801},rows.Select(row => row.SequenceId));
        (await db.GetLastFuturesItiSignalTrendDirectionChangeAsync(first.ContractId,date)).Should().BeEquivalentTo(latest, options => options.Excluding(row => row.BandAnchorPrice).Excluding(row => row.BandPercentage).Excluding(row => row.BandSize));
        Assert.Equal(801,(await db.GetFuturesItiTimeFrameStateAsync(first.ContractId,TimeFrameType.Daily,date))!.SequenceId);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand("ALTER TABLE market_data.futures_iti_timeframe_state ADD CONSTRAINT iti_rollback_probe CHECK (sequenceId <> 802)",connection))
            await command.ExecuteNonQueryAsync();
        Func<Task> fail = () => db.InsertFuturesItiSignalAsync(latest with { SequenceId = 802 });
        await fail.Should().ThrowAsync<Exception>();
        Assert.Equal(2,(await db.GetFuturesItiSignalsForContractAsync(first.ContractId,date,date)).Count);
        Assert.Equal(801,(await db.GetFuturesItiTimeFrameStateAsync(first.ContractId,TimeFrameType.Daily,date))!.SequenceId);
        await db.DeleteFuturesItiSignalAsync(first.ContractId,date,TimeFrameType.Daily);
        Assert.Empty(await db.GetFuturesItiSignalsForContractAsync(first.ContractId,date,date));
        Assert.Null(await db.GetFuturesItiTimeFrameStateAsync(first.ContractId,TimeFrameType.Daily,date));
    }

    static async Task VerifySecuritiesContractsAsync(string connectionString, IDbContextFactory factory, Dictionary<Type,IObjectRepository> repositories)
    {
        var settings = new DbConnectionSettings().Add("SecuritiesDbConnection",connectionString,"System.Data.Postgres");
        await new Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var db = new Storage.SecuritiesDb.SecuritiesDbContext(settings,factory,NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<Storage.SecuritiesDb.SecuritiesDbContext>),db);
        var first = new FuturesContractV3ReadModel("ES-NATIVE-1","First","ES-NATIVE","local1","FUT","USD","CME","50",new DateOnly(2026,12,18),true) with { Rollover = true };
        var second = first with { ContractId = "ES-NATIVE-2", LastTradeDate = new DateOnly(2027,3,19), OnTheRun = false };
        await db.InsertFuturesContractAsync(first);
        await db.InsertFuturesContractAsync(second);
        (await db.GetFuturesContractAsync(first.ContractId)).Should().BeEquivalentTo(first);
        Assert.Equal(first.ContractId,(await db.GetOnTheRunFuturesContractAsync(first.Symbol))!.ContractId);
        Assert.Equal(2,(await db.GetRolloverFuturesContractsAsync(first.Symbol)).Count);
        Assert.Equal(2,(await db.GetFuturesContractsBySymbolAsync(first.Symbol)).Count);
        Assert.Single(await db.GetFuturesContractsByIdsAsync([first.ContractId],first.Symbol));
        var option = new FuturesOptionContractReadModel("ES20261120P6000","Put",first.Symbol,"localp","FOP","USD","CME","50",new DateOnly(2026,11,20),6000,"Put");
        await db.InsertFuturesOptionContractAsync(option);
        (await db.GetFuturesOptionContractAsync(option.ContractId)).Should().BeEquivalentTo(option);
        Assert.Single(await db.GetFuturesOptionContractsByIdsAsync([option.ContractId]));
        await db.EnsureFuturesContractRolloverRowsAsync([first.Symbol],new DateTime(2026,10,7,12,0,0,DateTimeKind.Utc),"initial");
        await db.EnsureFuturesContractRolloverRowsAsync([first.Symbol],new DateTime(2026,10,8,12,0,0,DateTimeKind.Utc),"later");
        Assert.Equal("initial",(await db.GetFuturesContractRolloverAsync(first.Symbol))!.CreatedBy);
        await db.InsertFuturesOptionContractAsync(option with { ContractId = "ES20261120P6050",StrikePrice = 6050 });
        await db.InsertFuturesOptionContractAsync(option with { ContractId = "ES20261120P6100",StrikePrice = 6100 });
        var request = new TomasAI.IFM.Domain.MarketData.Shared.QueryParameters.GetFuturesOptionContractsPageParameter(first.Symbol,1);
        var firstPage = await db.GetFuturesOptionContractsPageAsync(request);
        Assert.Equal(option.ContractId,firstPage.Items.Single().ContractId);
        Assert.True(firstPage.HasMore);
        var secondPage = await db.GetFuturesOptionContractsPageAsync(request with { ContinuationToken = firstPage.ContinuationToken });
        Assert.Equal("ES20261120P6050",secondPage.Items.Single().ContractId);
        var thirdPage = await db.GetFuturesOptionContractsPageAsync(request with { ContinuationToken = secondPage.ContinuationToken });
        Assert.Equal("ES20261120P6100",thirdPage.Items.Single().ContractId);
        Assert.False(thirdPage.HasMore);
        Func<Task> wrongCursor = () => db.GetFuturesOptionContractsPageAsync(request with { Symbol = "OTHER",ContinuationToken = firstPage.ContinuationToken });
        await wrongCursor.Should().ThrowAsync<ArgumentException>();
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using(var command = new NpgsqlCommand("ALTER TABLE securities.futures_contract_by_symbol ADD CONSTRAINT securities_rollback_probe CHECK (contractId <> 'ES-NATIVE-FAIL')",connection))
            await command.ExecuteNonQueryAsync();
        Func<Task> fail = () => db.InsertFuturesContractAsync(first with { ContractId = "ES-NATIVE-FAIL" });
        await fail.Should().ThrowAsync<Exception>();
        Assert.Null(await db.GetFuturesContractAsync("ES-NATIVE-FAIL"));
        Assert.Equal(2,(await db.GetFuturesContractsBySymbolAsync(first.Symbol)).Count);
        await db.DeleteFuturesOptionContractAsync(option.ContractId);
        Assert.Null(await db.GetFuturesOptionContractAsync(option.ContractId));
        await db.DeleteFuturesContractAsync(second.ContractId);
        Assert.Single(await db.GetFuturesContractsBySymbolAsync(first.Symbol));
    }

    static async Task VerifyEconomicCalendarEventsAsync(Storage.MarketDataDb.MarketDataDbContext db, string connectionString)
    {
        var stamp = new DateTime(2026,10,7,14,0,0,DateTimeKind.Utc);
        var first = new EconomicCalendarReadModel(stamp,"US","Employment",null,"100","90",stamp,"native-test");
        var commandId = Guid.NewGuid();
        await db.InsertEconomicCalendarsAsync([first],ImportDuplicatePolicy.Reject,commandId);
        await db.InsertEconomicCalendarsAsync([first],ImportDuplicatePolicy.Reject,commandId);
        (await db.GetEconomicCalendarAsync(first.Id)).Should().BeEquivalentTo(first);
        Func<Task> conflict = () => db.InsertEconomicCalendarsAsync([first],ImportDuplicatePolicy.Reject,Guid.NewGuid());
        await conflict.Should().ThrowAsync<MarketDataImportDuplicateException>();
        var page = await db.GetEconomicCalendarPageAsync(new TomasAI.IFM.Domain.MarketData.Shared.QueryParameters.EconomicCalendarPageRequest { StartDateUtc = stamp.AddDays(-1), EndDateUtc = stamp.AddDays(1), CountryCodes = ["US"], PageSize = 10 });
        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(first);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using(var query = new NpgsqlCommand("ALTER TABLE market_data.economic_calendar_country_code ADD CONSTRAINT calendar_rollback_probe CHECK (countryCode <> 'ZZ')",connection))
            await query.ExecuteNonQueryAsync();
        var failed = first with { CountryCode = "ZZ" };
        Func<Task> fail = () => db.UpdateEconomicCalendarAsync(first.Id,failed);
        await fail.Should().ThrowAsync<Exception>();
        (await db.GetEconomicCalendarAsync(first.Id)).Should().BeEquivalentTo(first);
        Assert.Null(await db.GetEconomicCalendarAsync(failed.Id));
        await db.DeleteEconomicCalendarAsync(first.Id);
        Assert.Null(await db.GetEconomicCalendarAsync(first.Id));
    }

    static async Task VerifyOptionVolatilityAsync(Storage.MarketDataDb.MarketDataDbContext db, string connectionString)
    {
        var liveEnvironment = Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        liveEnvironment.EnvironmentName.Returns("isolated-live-iv-test");
        using var collector = new TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector(
            db, liveEnvironment, NullLogger<TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility.LiveOptionChainIvCollector>.Instance);
        var liveAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        EvaluatedOptionContractReadModel LiveLeg(bool call) => new(call ? "test-call" : "test-put", 6000m, call,
            10m, 11m, 1, 1, null, null, call ? .20 : .30, 10.5, .5, null, null, null, null,
            null, null, true, false, liveAt, null, liveAt);
        var liveChain = new EvaluatedOptionChainReadModel("test-ES", new(2026, 12, 18), 6000m, null, null,
            "Live", liveAt, [LiveLeg(true), LiveLeg(false)]);
        await collector.PublishAsync(liveChain, new(2026, 10, 12));
        var persistedLive = (await collector.GetLatestAsync(liveChain, default)).Metric!.Snapshot;
        Assert.Equal(.25m, persistedLive.CurrentImpliedVolatility);
        Assert.Equal(VolatilityMetricStatus.InsufficientHistory, persistedLive.RankStatus);
        Assert.NotNull(await db.GetSnapshotAsync(liveEnvironment.EnvironmentName, persistedLive.SnapshotId));
        var serializer = TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared;
        var metricsChain = liveChain with { BollingerStandardDeviation20 = 12.5m, PreviousSessionClose = 5990m,
            IvRank = 50m, IvRankStatus = "Qualified", BollingerAsOfUtc = liveAt };
        var roundTrip = serializer.Deserialize<EvaluatedOptionChainReadModel>(serializer.Serialize(metricsChain)!);
        Assert.Equal(12.5m, roundTrip!.BollingerStandardDeviation20);
        Assert.Equal(50m, roundTrip.IvRank);
        Assert.Equal(5990m, roundTrip.PreviousSessionClose);
        const string environment = "native-volatility-test";
        var scope = new VolatilityStorageScope(environment,new("ES-ATM-30D","method-v1"),"metric-v1");
        var source = VolatilityObservation("observation-1",scope.Series,1,null,VolatilityAt(0));
        var correctionSource = VolatilityObservation("observation-2",scope.Series,2,source.ObservationId,VolatilityAt(2));
        var first = VolatilityPublication(environment,"snapshot-1",1,source,1,null);
        var correction = VolatilityPublication(environment,"snapshot-2",2,correctionSource,2,"snapshot-1");
        await db.PublishAsync(first);
        await db.PublishAsync(first);
        await db.PublishAsync(correction);
        await db.PublishAsync(first);
        Assert.Equal("snapshot-2",(await db.GetLatestAsync(new(scope,VolatilityAt(3),TimeSpan.FromMinutes(2)))).Metric!.Snapshot.SnapshotId);
        Assert.Equal("snapshot-1",(await db.GetSnapshotAsync(environment,"snapshot-1"))!.Snapshot.SnapshotId);
        Assert.Equal("snapshot-1",(await db.GetMetricHistoryAsync(VolatilityHistory(scope,VolatilityHistoricalMode.AsKnown,VolatilityAt(1)))).Items.Single().Snapshot.SnapshotId);
        Assert.Equal("snapshot-2",(await db.GetMetricHistoryAsync(VolatilityHistory(scope,VolatilityHistoricalMode.Restated,null))).Items.Single().Snapshot.SnapshotId);
        Assert.Equal("observation-2",(await db.GetObservationHistoryAsync(VolatilityHistory(scope,VolatilityHistoricalMode.Restated,null))).Items.Single().ObservationId);
        Func<Task> conflict = () => db.PublishAsync(first with { Metric = first.Metric with { PublicationSequence = 99 } });
        await conflict.Should().ThrowAsync<InvalidOperationException>();
        var nextSource = VolatilityObservation("observation-next",scope.Series,1,null,VolatilityAt(3)) with { ExchangeValueDate = new DateOnly(2026,9,19) };
        await db.PublishAsync(VolatilityPublication(environment,"snapshot-next",3,nextSource,1,null));
        var request = VolatilityHistory(scope,VolatilityHistoricalMode.Restated,null) with { ToValueDate = new DateOnly(2026,9,19), PageSize = 1 };
        var page = await db.GetMetricHistoryAsync(request);
        Assert.Equal("snapshot-2",page.Items.Single().Snapshot.SnapshotId);
        Assert.NotNull(page.PagingState);
        var next = await db.GetMetricHistoryAsync(request with { PagingState = page.PagingState });
        Assert.Equal("snapshot-next",next.Items.Single().Snapshot.SnapshotId);
        Assert.Null(next.PagingState);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using(var command = new NpgsqlCommand("ALTER TABLE market_data.option_iv_metric_history ADD CONSTRAINT volatility_rollback_probe CHECK (publication_sequence <> 999)",connection))
            await command.ExecuteNonQueryAsync();
        var failedSource = VolatilityObservation("observation-fail",scope.Series,1,null,VolatilityAt(4));
        Func<Task> fail = () => db.PublishAsync(VolatilityPublication(environment,"snapshot-fail",999,failedSource,1,null));
        await fail.Should().ThrowAsync<Exception>();
        Assert.Null(await db.GetSnapshotAsync(environment,"snapshot-fail"));
        await using(var command = new NpgsqlCommand("SELECT count(*) FROM market_data.option_iv_observation_by_id WHERE observation_id='observation-fail'",connection))
            Assert.Equal(0L,(long)(await command.ExecuteScalarAsync())!);
        Assert.Equal("snapshot-next",(await db.GetLatestAsync(new(scope,VolatilityAt(4),TimeSpan.FromMinutes(2)))).Metric!.Snapshot.SnapshotId);
    }

    static OptionIvObservation VolatilityObservation(string id, VolatilitySeriesIdentity series, int revision,
        string? supersedes, DateTimeOffset available) =>
        new(1, id, series, new(2026, 9, 18), "daily-close", 0.20m + revision / 100m,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, string.Empty,
            available.AddMinutes(-1), available.AddSeconds(-1), available, revision, supersedes,
            new([], "black76-v1", $"digest-{id}", "evidence-v1"));

    static OptionIvPublication VolatilityPublication(string environment, string id, long sequence,
        OptionIvObservation source, int revision, string? supersedes) =>
        new(environment,
            new(new(1, id, $"digest-{id}", source.Series, "metric-v1", source.ExchangeValueDate,
                    source.SamplingSlot, source.ImpliedVolatility, VolatilityValueUnit.AnnualDecimal,
                    50m, VolatilityMetricStatus.Qualified, 50m, VolatilityMetricStatus.Qualified,
                    VolatilityMetricUnit.PercentagePoints0To100, 0.10m, 0.40m, 1, 0, 2, 2, 1m,
                    new(2026, 9, 16), new(2026, 9, 17), [source.ObservationId], $"sources-{id}",
                    "calculator-v1", source.ObservedAtUtc, source.AvailableAtUtc.AddSeconds(-2),
                    source.AvailableAtUtc.AddSeconds(-1), source.AvailableAtUtc),
                revision, supersedes, sequence),
            [source]);

    static VolatilityHistoryPageRequest VolatilityHistory(VolatilityStorageScope scope,
        VolatilityHistoricalMode mode, DateTimeOffset? knownAt) =>
        new(scope, new(2026, 9), new(2026, 9, 18), new(2026, 9, 18), null, mode, knownAt, 50, null);

    static DateTimeOffset VolatilityAt(int minute) => new(2026, 9, 18, 20, minute, 0, TimeSpan.Zero);

    [Fact]
    public async Task Download_outcome_source_projection_and_notification_marker_commit_or_roll_back_together()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("native-download-atomic");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection",infrastructure.PostgresConnectionString,"System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection,infrastructure.PostgresConnectionString,"System.Data.Postgres");
        await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var repositories = new Dictionary<Type,IObjectRepository>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var db = new Storage.MarketDataDb.MarketDataDbContext(settings,factory,
            new BlackboardService(Substitute.For<IRedisCache>(),new SystemTextJsonSerializer()),
            Substitute.For<ISequenceIdGenerator>(),NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<Storage.MarketDataDb.MarketDataDbContext>),db);
        var transactions = new PostgresEventTransaction(settings);
        var requested = new DateTime(2026,10,7,12,0,0,DateTimeKind.Utc);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = MarketDataDownloadDataset.TreasuryCurve, Scope = "US", ValueDate = new(2026,10,7),
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(), RequestedAtUtc = requested,
            StartedAtUtc = requested.AddSeconds(1), FinishedAtUtc = requested.AddSeconds(2),
            Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 1, PersistedRecordCount = 1, ElapsedMilliseconds = 1000
        };
        async Task Commit(MarketDataDownloadOutcome row,string stream)
        {
            var source = new MarketDataDownloadLogInsertedEvent { Id = Guid.NewGuid(),
                CommandId = MarketDataDownloadOutcome.LoggingCommandId(row.ImportCommandId),Outcome = row,
                PayloadSha256 = row.ComputeHash(),ReceivedOn = requested };
            await transactions.ExecuteOnceAsync(async (transaction,token) =>
            {
                await transaction.AppendAsync(stream,source.CommandId,source,0,token);
                await db.ProjectDownloadLogAsync(transaction,row,source.CommandId,source.PayloadSha256,token);
                return true;
            });
        }
        var successful = "download-atomic-success-" + Guid.NewGuid();
        await Commit(outcome,successful);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(infrastructure.PostgresConnectionString);
        await connection.OpenAsync();
        async Task<long> Count(string stream)
        {
            await using var query = new NpgsqlCommand("SELECT count(*) FROM event_log l JOIN event_stream_id s ON s.eventstreamid=l.eventstreamid WHERE s.eventstream=$1",connection);
            query.Parameters.AddWithValue(stream);
            return (long)(await query.ExecuteScalarAsync())!;
        }
        Assert.Equal(1L,await Count(successful));
        await using(var guard = new NpgsqlCommand("ALTER TABLE market_data.market_data_download_log ADD CONSTRAINT download_rollback_probe CHECK(persisted_record_count <> 2)",connection))
            await guard.ExecuteNonQueryAsync();
        var failed = outcome with { DownloadedRecordCount = 2,PersistedRecordCount = 2,ImportCommandId = Guid.NewGuid() };
        var failedStream = "download-atomic-failure-" + Guid.NewGuid();
        Func<Task> write = () => Commit(failed,failedStream);
        await write.Should().ThrowAsync<PostgresException>();
        Assert.Equal(0L,await Count(failedStream));
        await using var rows = new NpgsqlCommand("SELECT count(*) FROM market_data.market_data_download_log",connection);
        Assert.Equal(1L,(long)(await rows.ExecuteScalarAsync())!);
        await using var markers = new NpgsqlCommand("SELECT count(*) FROM event_projector_state",connection);
        Assert.Equal(1L,(long)(await markers.ExecuteScalarAsync())!);
        var calendar = new EconomicCalendarReadModel(requested,"US","Accepted source",null,"100","90",requested,"test");
        var economicSource = new TomasAI.IFM.Domain.MarketData.Shared.Events.EconomicCalendarsImportedCompleteEvent
        { Id = Guid.NewGuid(),CommandId = Guid.NewGuid(),EconomicCalendars = [calendar],ReceivedOn = requested,DuplicatePolicy = ImportDuplicatePolicy.Overwrite };
        var curve = SampleData.YieldCurveRate with { ValueDate = new DateOnly(2026,10,7) };
        var treasurySource = new TomasAI.IFM.Domain.MarketData.Shared.Events.YieldCurveRatesImportedCompleteEvent
        { Id = Guid.NewGuid(),CommandId = Guid.NewGuid(),YieldCurveRates = [curve],ReceivedOn = requested,DuplicatePolicy = ImportDuplicatePolicy.Overwrite };
        await transactions.ExecuteOnceAsync(async (transaction,token) =>
        {
            await transaction.AppendAsync("calendar-accepted-test",economicSource.CommandId,economicSource,0,token);
            await Storage.MarketDataDb.MarketDataDbContextExtensions.ImportEconomicCalendarsAsync(transaction,[calendar],economicSource.DuplicatePolicy,economicSource.CommandId,token);
            await transaction.AppendAsync("treasury-accepted-test",treasurySource.CommandId,treasurySource,0,token);
            await Storage.MarketDataDb.MarketDataDbContextExtensions.ImportYieldCurvesAsync(transaction,[curve],treasurySource.DuplicatePolicy,treasurySource.CommandId,requested,token);
            return true;
        });
        var sources = new List<TomasAI.IFM.Shared.EventSourcing.IEvent>();
        await using(var query = new NpgsqlCommand("SELECT n.EventTypeName,l.EventVersion,l.EventPayload FROM event_log l JOIN event_name_id n ON l.EventNameId=n.EventNameId ORDER BY l.EventVersion",connection))
        await using(var reader = await query.ExecuteReaderAsync())
            while(await reader.ReadAsync()) sources.Add(new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel { EventTypeName = reader.GetString(0),EventVersion = reader.GetInt64(1),EventData = reader.GetFieldValue<byte[]>(2) }.ToDomainEvent());
        foreach(var table in new[] { "economic_calendar","economic_calendar_country_code","yield_curve_rates","yield_curve_rate_by_date","yield_curve_rate_year" })
        {
            await using var clear = new NpgsqlCommand("DELETE FROM market_data." + table,connection);
            await clear.ExecuteNonQueryAsync();
        }
        async IAsyncEnumerable<TomasAI.IFM.Shared.EventSourcing.IEvent> AcceptedSources()
        {
            foreach(var source in sources) { yield return source; await Task.Yield(); }
        }
        var rebuilder = new TomasAI.IFM.Application.EventProjector.ReadProjectionRebuilder(
            TomasAI.IFM.Application.EventProjector.AcceptedMarketDataReadProjectionDescriptors.Create(factory,transactions));
        Assert.Equal(3L,await rebuilder.RebuildAsync(AcceptedSources()));
        (await db.GetEconomicCalendarAsync(calendar.Id)).Should().BeEquivalentTo(calendar);
        (await db.GetYieldCurveRateAsync(curve.ValueDate)).Should().BeEquivalentTo(curve);
    }

    [Fact]
    public async Task Session_snapshots_and_every_admitted_history_row_commit_together_and_roll_back_together()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("native-eod");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add("MarketDataDbConnection", infrastructure.PostgresConnectionString, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var repositories = new Dictionary<Type, IObjectRepository>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var board = new BlackboardService(Substitute.For<IRedisCache>(), new SystemTextJsonSerializer());
        var observationIds = Substitute.For<ISequenceIdGenerator>();
        observationIds.GetSequenceIdAsync(SequenceName.FuturesOptionTickPriceData_TickId).Returns(100L, 101L);
        var db = new Storage.MarketDataDb.MarketDataDbContext(settings, factory, board,
            observationIds, NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<Storage.MarketDataDb.MarketDataDbContext>), db);
        await VerifyReferenceCatalogAsync(infrastructure.PostgresConnectionString, factory, repositories);
        await VerifySecuritiesContractsAsync(infrastructure.PostgresConnectionString, factory, repositories);
        var date = new DateOnly(2026, 10, 7);
        await VerifyIndicatorObservationsAsync(db, date);
        await VerifyRegimeIndicatorObservationsAsync(db);
        await VerifyVwapObservationsAsync(db, date);
        await VerifyBarClosingAndVxObservationsAsync(db, date);
        await VerifyCalculatedOptionPricesAsync(db, date);
        await VerifyOptionChainAndReturnsAsync(db,date);
        await VerifyVixEodAsync(db,date,infrastructure.PostgresConnectionString);
        await VerifyTreasuryCurveAsync(db,date,infrastructure.PostgresConnectionString);
        await VerifyTradeSignalsAsync(db,date,infrastructure.PostgresConnectionString);
        await VerifyItiSignalsAsync(db,date,infrastructure.PostgresConnectionString);
        await VerifyEconomicCalendarEventsAsync(db,infrastructure.PostgresConnectionString);
        await VerifyOptionVolatilityAsync(db,infrastructure.PostgresConnectionString);
        await VerifyRsiObservationsAsync(db, date);
        await VerifySnapshotAndEvidenceAsync(db, date);
        await VerifyDownloadAndFeedStateAsync(db,date);
        await VerifyCalendarAsync(db, date);
        await VerifyHistoricalObservationsAsync(db);
        await VerifyCanonicalQueryMaintenanceAsync(db, infrastructure.PostgresConnectionString);
        var first = SampleData.FuturesEodData with { ContractId = "ES", ValueDate = date, ClosePrice = 6000.125m, Volume = 12345, MarketDirectionIndicator = 0.125 };
        var second = first with { ClosePrice = 6001.25m, Volume = 12346 };
        var rows = new[]
        {
            new BufferedFuturesEodRow(first, true) { HistorySequenceId = 1 },
            new BufferedFuturesEodRow(second, true) { HistorySequenceId = 2 }
        };
        await db.PersistRealtimeFuturesEodBatchAsync(rows);
        Assert.Equal(second.ClosePrice, (await db.GetFuturesEodDataAsync("ES", date))!.ClosePrice);
        var history = await db.GetFuturesIntraDayDataAsync("ES", date);
        Assert.Equal(new long[] { 2, 1 }, history.Select(x => x.SequenceId));
        Assert.All(history, row => Assert.Equal(0.125, row.MarketDirectionIndicator));
        await db.PersistRealtimeFuturesEodBatchAsync(rows);
        Assert.Equal(2, (await db.GetFuturesIntraDayDataAsync("ES", date)).Count);
        await db.InsertFuturesEodDataAsync(new[] { first with { ValueDate = date.AddDays(-1) } });
        Assert.Equal(date.AddDays(-1), (await db.GetLastFuturesEodDataAsync("ES", date))!.ValueDate);

        Assert.Equal(new[] {date,date.AddDays(-1)},(await db.GetCurrentFuturesEodDataByDateRangeAsync(date.AddDays(-1),date)).Select(row => row.ValueDate));
        Assert.Equal(date, (await db.GetCurrentFuturesEodDataAsync(date))!.ValueDate);
        Assert.Equal(2, (await db.GetFuturesEodDataByDateRangeAsync("ES", date.AddDays(-1), date)).Count);
        Assert.Equal(2, (await db.GetFuturesEodClosingPricesAsync("ES", first.Symbol, date.AddDays(-1), date, 10)).Count);
        var signal = SampleData.FuturesTdiSignal with { ContractId = "ES", ValueDate = date, Timestamp = new TimeOnly(12, 0) };
        await db.InsertFuturesTdiSignalAsync(signal);
        await db.InsertFuturesTdiSignalAsync(signal with { Timestamp = new TimeOnly(12, 1), Rsi = 70.25 });
        var latestSignal = await db.GetLastFuturesTdiSignalAsync("ES", date, signal.TimePeriod, signal.ConfigurationId);
        Assert.NotNull(latestSignal);
        Assert.Equal(new TimeOnly(12, 1), latestSignal.Timestamp);
        Assert.Equal(70.25, latestSignal.Rsi);

        await db.DeleteFuturesEodDataAsync("ES", date.AddDays(-1));
        Assert.Null(await db.GetLastFuturesEodDataAsync("ES", date));
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(infrastructure.PostgresConnectionString);
        await connection.OpenAsync();
        await using (var constraint = new NpgsqlCommand("ALTER TABLE market_data.futures_intra_day_data ADD CONSTRAINT reject_test_history CHECK (sequenceId <> 999);", connection))
            await constraint.ExecuteNonQueryAsync();
        var rejected = second with { ContractId = "FAIL", ClosePrice = 7000m };
        await Assert.ThrowsAnyAsync<Exception>(() => db.PersistRealtimeFuturesEodBatchAsync(new[]
        {
            new BufferedFuturesEodRow(rejected, true) { HistorySequenceId = 999 }
        }));
        await using var verify = new NpgsqlCommand("SELECT (SELECT count(*) FROM market_data.futures_eod_data WHERE contractId='FAIL') + (SELECT count(*) FROM market_data.futures_eod_data_by_month WHERE contractId='FAIL') + (SELECT count(*) FROM market_data.futures_eod_data_index WHERE contractId='FAIL');", connection);
        Assert.Equal(0L, (long)(await verify.ExecuteScalarAsync())!);
    }
    static async Task VerifyIndicatorObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var atr = new FuturesAtrSignalReadModel
        {
            ContractId = "ATR", ValueDate = date, TimePeriod = TimeFrameType.FifteenMinutes,
            PeriodLength = 14, Timestamp = new TimeOnly(12, 0), FuturesPrice = 6000.125m,
            AtrValue = 12.75, TrueRange = 15.25, PreviousAtrValue = 12.5,
            AtrBaseline = 11.75, AtrRatio = 12.75 / 11.75, IsWarm = true
        };
        await db.InsertFuturesAtrSignalAsync(atr);
        await db.InsertFuturesAtrSignalAsync(atr with { Timestamp = new TimeOnly(12, 1), AtrValue = 13.5 });
        await db.InsertFuturesAtrSignalAsync(atr with { ValueDate = date.AddDays(-1), AtrValue = 99 });
        var latestAtr = await db.GetLastFuturesAtrSignalAsync("ATR", date, atr.TimePeriod, 14);
        Assert.Equal(13.5, latestAtr!.AtrValue);
        Assert.Equal(6000.125m, latestAtr.FuturesPrice);
        Assert.Equal(atr.AtrRatio, latestAtr.AtrRatio);
        Assert.True(latestAtr.IsWarm);
        Assert.Equal(date, (await db.GetLastFuturesAtrDailySignalAsync("ATR", atr.TimePeriod, 14))!.ValueDate);
        await db.InsertFuturesAtrSignalAsync(atr with { Timestamp = new TimeOnly(12, 1), AtrValue = 14.25 });
        Assert.Equal(14.25, (await db.GetLastFuturesAtrSignalAsync("ATR", date, atr.TimePeriod, 14))!.AtrValue);
        await db.DeleteFuturesAtrSignalAsync("ATR", date, atr.TimePeriod, 14);
        Assert.Null(await db.GetLastFuturesAtrSignalAsync("ATR", date, atr.TimePeriod, 14));
        Assert.Equal(date.AddDays(-1), (await db.GetLastFuturesAtrDailySignalAsync("ATR", atr.TimePeriod, 14))!.ValueDate);

        var adx = new FuturesAdxSignalReadModel
        {
            ContractId = "ADX", ValueDate = date, TimePeriod = atr.TimePeriod, PeriodLength = 14,
            Timestamp = new TimeOnly(12, 0), FuturesPrice = 6000.125m,
            PlusDI = 22.75, MinusDI = 10.5, AdxValue = 30.25, IsWarm = true
        };
        await db.InsertFuturesAdxSignalAsync(adx);
        await db.InsertFuturesAdxSignalAsync(adx with { Timestamp = new TimeOnly(12, 1), AdxValue = 31.5 });
        await db.InsertFuturesAdxSignalAsync(adx with { ValueDate = date.AddDays(-1), AdxValue = 99 });
        Assert.Equal(31.5, (await db.GetLastFuturesAdxSignalAsync("ADX", date, atr.TimePeriod, 14))!.AdxValue);
        Assert.True((await db.GetLastFuturesAdxSignalAsync("ADX", date, atr.TimePeriod, 14))!.IsWarm);
        Assert.Equal(date, (await db.GetLastFuturesAdxDailySignalAsync("ADX", atr.TimePeriod, 14))!.ValueDate);
        await db.DeleteFuturesAdxSignalAsync("ADX", date, atr.TimePeriod, 14);
        Assert.Null(await db.GetLastFuturesAdxSignalAsync("ADX", date, atr.TimePeriod, 14));

        var macd = new FuturesMacdSignalReadModel
        {
            ContractId = "MACD", ValueDate = date, TimePeriod = atr.TimePeriod,
            SignalEmaPeriod = 9, FastEmaPeriod = 12, SlowEmaPeriod = 26,
            Timestamp = new TimeOnly(12, 0), FuturesPrice = 6000.125m,
            FastEma = 6000.5, SlowEma = 5990.25, MacdLine = 10.25, SignalLine = 9.5,
            Histogram = 0.75, IsWarm = true, ObservationCount = 35
        };
        await db.InsertFuturesMacdSignalAsync(macd);
        await db.InsertFuturesMacdSignalAsync(macd with { Timestamp = new TimeOnly(12, 1), Histogram = 1.25 });
        await db.InsertFuturesMacdSignalAsync(macd with { ValueDate = date.AddDays(-1), Histogram = 99 });
        var latestMacd = await db.GetLastFuturesMacdSignalAsync("MACD", date, atr.TimePeriod, 9, 12, 26);
        Assert.Equal(1.25, latestMacd!.Histogram);
        Assert.Equal(macd.FastEma, latestMacd.FastEma);
        Assert.Equal(35, latestMacd.ObservationCount);
        Assert.True(latestMacd.IsWarm);
        Assert.Equal(date, (await db.GetLastFuturesMacdDailySignalAsync("MACD", atr.TimePeriod, 9, 12, 26))!.ValueDate);
    }

    static async Task VerifyRegimeIndicatorObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db)
    {
        var valueDate = new DateOnly(2098, 3, 15);
        var asOf = new DateTimeOffset(2098, 3, 14, 21, 59, 59, TimeSpan.Zero);
        var series = MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId(
            $"T{Guid.NewGuid():N}"[..8], "calendar-front", "unadjusted", 1));
        var observationId = new FuturesTradeSessionBarId(Guid.NewGuid());
        MarketAnalyticsSignalMetadata Metadata(
            MarketAnalyticsSignalKind kind,
            string configurationId) => new()
            {
                SignalKey = new(series, kind, TimeFrameType.Daily, configurationId),
                ContractId = "ESH98",
                ValueDate = valueDate,
                ObservationId = observationId,
                MarketDataAsOfUtc = asOf,
                CalculatedAtUtc = asOf.AddSeconds(1),
                SourceSequence = 901,
                SchemaVersion = 1,
                CalculationVersion = "integration-v1",
                CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate,
                IsValid = true
            };
        var ema = new FuturesEmaSignalReadModel
        {
            Metadata = Metadata(MarketAnalyticsSignalKind.Ema, "ema-10-20-50-200-v1"),
            Price = 6_100m,
            Ema10 = 6_090m,
            PreviousEma10 = 6_088m,
            Ema10Slope = 2m,
            Ema20 = 6_080m,
            Ema50 = 6_050m,
            Ema200 = 5_900m,
            IsWarm = true
        };
        var bb = new FuturesBbSignalReadModel
        {
            Metadata = Metadata(
                MarketAnalyticsSignalKind.BollingerBand,
                "bb-10-20-ema-center-population-v1"),
            Price = 6_100m,
            Ema10Center = 6_090m,
            StandardDeviation10 = 10m,
            Upper10 = 6_110m,
            Lower10 = 6_070m,
            Width10 = 40m,
            Position10 = 0.75m,
            Ema20Center = 6_080m,
            StandardDeviation20 = 15m,
            Upper20 = 6_110m,
            Lower20 = 6_050m,
            Width20 = 60m,
            Position20 = 0.83m,
            Width20Baseline = 55m,
            Width20Ratio = 1.09m,
            IsWarm = true
        };

        await db.InsertFuturesEmaSignalAsync(ema);
        await db.InsertFuturesBollingerBandSignalAsync(bb);
        var intradayBb = bb with { Metadata = bb.Metadata with { SignalKey = bb.Metadata.SignalKey with
            { TimeFrame = TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.FifteenSeconds } } };
        await db.InsertFuturesBollingerBandSignalAsync(intradayBb);
        var intradayRead = await db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, valueDate,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.FifteenSeconds);
        Assert.Equal(intradayBb.StandardDeviation20, intradayRead!.StandardDeviation20);


        var loadedEma = await db.GetLatestFuturesEmaSignalAsync(
            series, valueDate);
        var loadedBb = await db.GetLatestFuturesBollingerBandSignalAsync(
            series, valueDate);

        loadedEma.Should().NotBeNull();
        loadedEma!.Metadata.Should().BeEquivalentTo(ema.Metadata);
        loadedEma.Should().BeEquivalentTo(ema, options => options.Excluding(value =>
            value.BaselineValueDate));
        loadedBb.Should().NotBeNull();
        loadedBb!.Metadata.Should().BeEquivalentTo(bb.Metadata);
        loadedBb.Should().BeEquivalentTo(bb, options => options.Excluding(value =>
            value.BaselineValueDate));
    }

    static async Task VerifyVwapObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var start = new DateTimeOffset(date, new TimeOnly(10, 0), TimeSpan.Zero);
        var snapshot = new FuturesVwapSignalReadModel
        {
            ContractId = "VWAP", ValueDate = date, ConfigurationId = "vwap-test",
            SessionStartUtc = start, SessionEndUtc = start.AddHours(7), AsOfUtc = start,
            LastTradeOrdinal = 1, LastTradeSourceSequence = 100,
            CumulativePriceVolume = 12000.25m, CumulativeVolume = 2, EligibleTradeCount = 1,
            LastPrice = 6000.125m, Vwap = 6000.125m, PriceMinusVwap = 0m,
            PriceToVwapPercent = 0m, StreamEpochId = Guid.NewGuid(), IsWarm = true,
            IsValid = true, IsTickExact = true
        };
        await db.InsertFuturesVwapSignalAsync(snapshot);
        var next = snapshot with { AsOfUtc = start.AddSeconds(1), LastTradeOrdinal = 2, Vwap = null, IsValid = false };
        await db.InsertFuturesVwapSignalAsync(next);
        await db.InsertFuturesVwapSignalAsync(next);
        var latest = await db.GetLatestFuturesVwapSignalAsync("VWAP", date, "vwap-test");
        latest.Should().BeEquivalentTo(next);
        var history = await db.GetFuturesVwapSignalHistoryAsync("VWAP", date, "vwap-test");
        Assert.Equal(2, history.Count);
        Assert.Equal(new long[] { 2, 1 }, history.Select(row => row.LastTradeOrdinal));
        Assert.Empty(await db.GetFuturesVwapSignalHistoryAsync("VWAP", date.AddDays(-1), "vwap-test"));
    }

    static async Task VerifyReferenceCatalogAsync(string connection, IDbContextFactory factory,
        Dictionary<Type, IObjectRepository> repositories)
    {
        var settings = new DbConnectionSettings().Add(ReferenceDbContext.ReferenceDbConnection, connection, "System.Data.Postgres");
        await new ReferenceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var ids = Substitute.For<ISequenceIdGenerator>();
        ids.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, Arg.Any<CancellationToken>()).Returns(100L);
        var context = new ReferenceDbContext(settings, factory, ids, NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<ReferenceDbContext>), context);
        await VerifyReferenceRiskAndJobsAsync(context, ids, connection);
        var baseline = new TradeStrategyFamilyReadModel
        {
            TradeStrategyFamilyId = 1, DefinitionVersion = 1, SystemKey = "native-reference-fixture",
            Family = TradeStrategyFamilyType.FuturesOption, Strategy = TradeStrategyType.IronCondor,
            TimeFrame = TimeFrameType.Monthly, Symbol = "ES", Currency = "USD",
            Description = "Reference fixture", State = TradeStrategyFamilyState.Active,
            CreatedOnUtc = DateTime.UtcNow, CreatedBy = "fixture"
        };
        await context.InsertTradeStrategyFamilyAsync(baseline);
        await context.InsertTradeStrategyFamilyAsync(baseline);
        Assert.Single(await context.GetTradeStrategyFamiliesAsync());
        var request = new CreateTradeStrategyFamilyRequest
        {
            OperationId = Guid.NewGuid(), Family = baseline.Family, Strategy = baseline.Strategy,
            TimeFrame = baseline.TimeFrame, TradeStrategySymbolId = 101, Description = "Published fixture"
        };
        var candidate = baseline with
        {
            SystemKey = TradeStrategyFamilyReadModel.ComposeSystemKey(baseline.Family, baseline.Strategy),
            TradeStrategySymbolId = 101, Exchange = "CME", Description = request.Description
        };
        var store = new TradeStrategyFamilyCatalogStore(factory, ids);
        var created = await store.CreateAsync(request, candidate, CancellationToken.None);
        var repeated = await store.CreateAsync(request, candidate, CancellationToken.None);
        Assert.Equal(created, repeated);
        Assert.Equal(100, created.TradeStrategyFamilyId);
        var definitions = await context.GetTradeStrategyFamiliesAsync();
        Assert.Equal(2, definitions.Count);
        Assert.Equal(101, definitions.Single(row => row.TradeStrategyFamilyId == 100).TradeStrategySymbolId);
        Assert.Equal("CME", definitions.Single(row => row.TradeStrategyFamilyId == 100).Exchange);
    }

    static async Task VerifyBarClosingAndVxObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var time = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var first = SampleData.FuturesBarData with { ContractId = "BARS", Symbol = "ES", ValueDate = date, BarDate = time, BarValue = 6000.125m };
        var second = first with { BarDate = time.AddMinutes(1), BarValue = 6001.25m };
        await db.InsertFuturesBarDataAsync(new[] { second, first });
        await db.InsertFuturesBarDataAsync(first);
        Assert.Equal(2, await db.GetFuturesBarDataCountAsync(first.Id));
        var bars = await db.GetFuturesBarDataAsync("BARS", "ES", date, time, time.AddMinutes(1));
        Assert.Equal(new decimal[] { 6000.125m, 6001.25m }, bars.Select(row => row.BarValue));
        Assert.Equal(second.BarValue, (await db.GetLastFuturesBarDataAsync("BARS", "ES", date)).BarValue);
        Assert.Equal(2, (await db.GetFuturesBarDataAsync()).Count);
        await db.DeleteFuturesBarDataAsync(first.Id);
        Assert.Equal(0, await db.GetFuturesBarDataCountAsync(first.Id));

        var close = new FuturesClosingPriceReadModel
        {
            ContractId = "CLOSE", ValueDate = date, ClosingPrice = 6000.125m,
            CreatedOn = time, CreatedBy = "fixture"
        };
        await db.InsertFuturesClosingPriceAsync(close);
        await db.InsertFuturesClosingPriceAsync(close with { ValueDate = date.AddDays(-1), ClosingPrice = 5999.25m });
        await db.InsertFuturesClosingPriceAsync(close with { ValueDate = date.AddDays(-2), ClosingPrice = 5998.125m });
        Assert.Equal(6000.125m, (await db.GetFuturesClosingPriceAsync(close.Id))!.ClosingPrice);
        Assert.Equal(5999.25m, (await db.GetYesterdaysFuturesClosingPriceAsync(close.Id))!.ClosingPrice);
        await db.DeleteFuturesClosingPriceAsync("CLOSE", date);
        Assert.Null(await db.GetFuturesClosingPriceAsync(close.Id));

        var vx = new FuturesVxTermStructureSignalReadModel
        {
            ValueDate = date, ConfigurationId = "vx-test", FrontVxContractId = "VXF", BackVxContractId = "VXB",
            FrontExpiry = date.AddDays(20), BackExpiry = date.AddDays(50), FrontVxPrice = 20.25m, BackVxPrice = 21.5m,
            FrontBackSpread = -1.25m, FrontBackRatio = 20.25m / 21.5m, TermStructurePercent = -5.8m,
            FrontSourceTimestampUtc = new(time), BackSourceTimestampUtc = new(time),
            FrontSourceSequence = 1, BackSourceSequence = 1, CalculatedAtUtc = new(time), IsWarm = true, IsValid = true
        };
        var next = vx with { CalculatedAtUtc = vx.CalculatedAtUtc.AddSeconds(1), FrontSourceSequence = 2, PriorFrontBackRatio = vx.FrontBackRatio };
        await db.InsertFuturesVxTermStructureSignalAsync(vx);
        await db.InsertFuturesVxTermStructureSignalAsync(next);
        await db.InsertFuturesVxTermStructureSignalAsync(next);
        (await db.GetLatestFuturesVxTermStructureSignalAsync(date, "vx-test")).Should().BeEquivalentTo(next);
        Assert.Null(await db.GetLatestFuturesVxTermStructureSignalAsync(date.AddDays(-1), "vx-test"));
    }

    static async Task VerifyCalculatedOptionPricesAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var first = SampleData.FuturesOptionTickData with
        {
            ContractId = "CALCULATED", ValueDate = date, TickTime = new TimeOnly(12, 0),
            OptionPrice = 12.125, BidPrice = 12.0, AskPrice = 12.25, Delta = -0.16, Gamma = 0.00125
        };
        var second = first with { TickTime = new TimeOnly(12, 1), OptionPrice = 13.25 };
        await db.InsertFuturesOptionTickPriceDataAsync(first);
        await db.InsertFuturesOptionTickPriceDataAsync(second);
        var stored = await db.GetFuturesOptionTickPriceDataAsync(new FuturesOptionTickDataId("CALCULATED", date, 100));
        Assert.Equal(12.125, stored!.OptionPrice);
        Assert.Equal(-0.16, stored.Delta);
        Assert.Equal(0.00125, stored.Gamma);
        Assert.Equal(13.25, (await db.GetLastFuturesOptionTickPriceDataAsync("CALCULATED", date))!.OptionPrice);
        await db.DeleteFuturesOptionTickPriceDataAsync("CALCULATED", date);
        Assert.Null(await db.GetLastFuturesOptionTickPriceDataAsync("CALCULATED", date));
    }

    static async Task VerifyRsiObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var time = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var first = SampleData.FuturesRsiSignal with
        {
            ContractId = "RSI", ValueDate = date, Timestamp = new TimeOnly(12, 0),
            Price = 6000.125m, RSI = 55.5, PreviousRsi = 54.5, RegimeSlope = 1,
            IsWarm = true, SourceSequence = 1, SourceEventTimestamp = time
        };
        var next = first with { Timestamp = new TimeOnly(12, 1), RSI = 60.25, SourceSequence = 2, SourceEventTimestamp = time.AddMinutes(1) };
        await db.InsertFuturesRsiSignalAsync(first);
        await db.InsertFuturesRsiSignalAsync(next);
        await db.InsertFuturesRsiSignalAsync(next);
        var latest = await db.GetLastFuturesRsiSignalAsync("RSI", date, first.TimePeriod, first.PeriodLength);
        latest.Should().BeEquivalentTo(next);
        var trend = await db.GetFuturesTrendDirectionFromRSISignalAsync("RSI", date, first.TimePeriod, first.PeriodLength,
            time.AddMinutes(1), 2, time, time.AddMinutes(1));
        Assert.Equal(2, trend.UpTrendCount);
        Assert.Equal(0, trend.DownTrendCount);
        Assert.Equal(FuturesTrendType.UpTrending, trend.TrendDirection);
        Assert.Null(await db.GetLastFuturesRsiSignalAsync("RSI", date.AddDays(-1), first.TimePeriod, first.PeriodLength));
    }

    static async Task VerifySnapshotAndEvidenceAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var snapshot = new MarketOutlookReadModel
        {
            ContractId = "OUTLOOK", ValueDate = date,
            UpdatedAtUtc = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
            FuturesEodData = SampleData.FuturesEodData with { ContractId = "OUTLOOK", ValueDate = date },
            MissingInputs = "fixture"
        };
        await db.UpsertMarketOutlookSnapshotAsync(snapshot, 1);
        var replacement = snapshot with { MissingInputs = "replacement" };
        await db.UpsertMarketOutlookSnapshotAsync(replacement, 2);
        (await db.GetMarketOutlookSnapshotAsync("OUTLOOK", date)).Should().BeEquivalentTo(replacement);
        var next = snapshot with { ValueDate = date.AddDays(1), FuturesEodData = snapshot.FuturesEodData with { ValueDate = date.AddDays(1) } };
        await db.UpsertMarketOutlookSnapshotAsync(next, 3);
        Assert.Equal(date.AddDays(1), (await db.GetMarketOutlookSnapshotAsync("OUTLOOK", date.AddDays(2)))!.ValueDate);
        Assert.Null(await db.GetMarketOutlookSnapshotAsync("OUTLOOK", date.AddDays(-1)));

        var source = new OptionTradeSource("GLBX.MDP3", 1, 100, "OPTION", date, 12.125m, 1, 7, 1000, 1001, Guid.NewGuid(), "raw-option");
        var evidence = new OptionTradeEvidence(source, null, null, DateTimeOffset.UtcNow, null,
            new OptionPricingFailure("FixtureFailure", "Quote", source.ContractId, "Isolated persistence test"));
        await db.WriteAsync(evidence, CancellationToken.None);
        await db.WriteAsync(evidence, CancellationToken.None);
        (await db.ReadAsync(source.ContractId, date, source.Identity, CancellationToken.None)).Should().BeEquivalentTo(evidence);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await db.WriteAsync(evidence with { Source = source with { Price = 13m } }, CancellationToken.None));
        Assert.Equal(12.125m, (await db.ReadAsync(source.ContractId, date, source.Identity, CancellationToken.None))!.Source.Price);

        var key = new CompositionPreparationKey(Guid.NewGuid(), 1, new string('a', 64));
        var service = new CompositionPreparationService(Substitute.For<ICompositionMarketDataApi>(), db);
        var prepared = await service.PrepareEmptyAsync(key, "ES", "Monthly", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None);
        Assert.Null(prepared.Failure);
        Assert.NotNull(prepared.Preparation);
        var persisted = await db.ReadAsync(key, CancellationToken.None);
        persisted.Should().BeEquivalentTo(prepared.Preparation);
        var repeated = await service.PrepareEmptyAsync(key, "ES", "Monthly", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None);
        Assert.Equal(prepared.Preparation!.Digest, repeated.Preparation!.Digest);
    }

    static async Task VerifyCalendarAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var first = new MarketHolidayReadModel(CurrencyType.USD, date, "Fixture holiday");
        var next = first with { HolidayDate = date.AddDays(1), Description = "Next holiday" };
        await db.InsertMarketHolidayAsync(next);
        await db.InsertMarketHolidayAsync(first);
        await db.InsertMarketHolidayAsync(first);
        var rows = await db.GetMarketHolidaysAsync(CurrencyType.USD);
        Assert.Equal(new[] { date, date.AddDays(1) }, rows.Select(row => row.HolidayDate));
        Assert.Single(await db.GetMarketHolidaysByDateRangeAsync(CurrencyType.USD, date, date));
        Assert.Empty(await db.GetMarketHolidaysAsync(CurrencyType.CAD));
        await db.DeleteMarketHolidayAsync(first);
        await db.DeleteMarketHolidayAsync(first);
        Assert.Single(await db.GetMarketHolidaysAsync(CurrencyType.USD));
        await db.DeleteMarketHolidaysAsync(CurrencyType.USD);
        Assert.Empty(await db.GetMarketHolidaysAsync(CurrencyType.USD));
    }

    static async Task VerifyReferenceRiskAndJobsAsync(ReferenceDbContext db, ISequenceIdGenerator ids, string connectionString)
    {
        var ratio = new MDIForwardLossRatioReadModel { MDI = 1, ForwardLossRatio = 0.25, CreatedBy = "fixture", UpdatedBy = "fixture" };
        await db.InsertMDIForwardLossRatiosAsync(new[] { ratio with { MDI = 2 }, ratio });
        var ratios = await db.GetMDIForwardLossRatiosAsync(ratio.TrendDirection, ratio.TradeType);
        Assert.Equal(new[] { 1, 2 }, ratios.Select(row => row.MDI));
        Assert.All(ratios, row => Assert.Null(row.CreatedOn));
        await db.InsertMDIForwardLossRatioAsync(ratio with { ForwardLossRatio = 0.75 });
        Assert.Equal(0.75, (await db.GetMDIForwardLossRatiosAsync(ratio.TrendDirection, ratio.TradeType, CancellationToken.None)).First().ForwardLossRatio);
        await db.DeleteMDIForwardLossRatioAsync(ratio.TrendDirection, ratio.TradeType);
        Assert.Empty(await db.GetMDIForwardLossRatiosAsync(ratio.TrendDirection, ratio.TradeType));

        ids.GetSequenceIdAsync(SequenceNameExtensions.ParseSequenceName("ScheduledJobId")).Returns(101L,102L,103L,104L);
        var first = TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb.SampleData.ScheduledJob with { JobName = "NativeDaily", JobScheduleDate = DateTime.UtcNow };
        await db.InsertScheduledJobAsync(first);
        await db.InsertScheduledJobAsync(first with { JobName = "nativedaily" });
        Assert.Equal(101, await db.GetScheduledJobIdAsync("NativeDaily"));
        Assert.Equal(102, await db.GetScheduledJobIdAsync("nativedaily", CancellationToken.None));
        Assert.Equal(0, await db.GetScheduledJobIdAsync("NATIVEDAILY"));
        var jobs = (await db.GetScheduledJobsAsync()).ToArray();
        Assert.Equal(new[] {101,102}, jobs.Select(row => row.JobId));
        Assert.All(jobs, row => Assert.Equal(row.JobId, row.DaysOfWeek!.JobId));
        Assert.True(jobs[0].DaysOfWeek!.Monday);
        await Assert.ThrowsAnyAsync<Exception>(() => db.UpdateScheduledJobAsync(jobs[0] with { JobName = "nativedaily" }));
        Assert.Equal(101, await db.GetScheduledJobIdAsync("NativeDaily"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.InsertScheduledJobAsync(first));
        Assert.Equal(2, (await db.GetScheduledJobsAsync()).Count);
        await db.UpdateScheduledJobAsync(jobs[0] with { JobName = "Renamed", DaysOfWeek = null, TaskEnabled = false });
        var renamed = (await db.GetScheduledJobsAsync(CancellationToken.None)).Single(row => row.JobId == 101);
        Assert.Null(renamed.DaysOfWeek);
        Assert.False(renamed.TaskEnabled);
        Assert.Equal(0, await db.GetScheduledJobIdAsync("NativeDaily"));
        Assert.Equal(101, await db.GetScheduledJobIdAsync("Renamed"));

        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var constraint = new NpgsqlCommand("ALTER TABLE reference.scheduled_job_days ADD CONSTRAINT reject_job_weekdays CHECK(jobId<>104);", connection))
            await constraint.ExecuteNonQueryAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => db.InsertScheduledJobAsync(first with { JobName = "RejectedWeekdays" }));
        Assert.Equal(0, await db.GetScheduledJobIdAsync("RejectedWeekdays"));
        await db.DeleteScheduledJobAsync(102);
        await db.DeleteScheduledJobAsync(102);
        Assert.Single(await db.GetScheduledJobsAsync());
        await db.DeleteScheduledJobAsync(101);
        Assert.Empty(await db.GetScheduledJobsAsync());
        await using var verify = new NpgsqlCommand("SELECT count(*) FROM reference.scheduled_job_days;", connection);
        Assert.Equal(0L,(long)(await verify.ExecuteScalarAsync())!);
    }

    static async Task VerifyHistoricalObservationsAsync(Storage.MarketDataDb.MarketDataDbContext db)
    {
        var series = MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var date = new DateOnly(2026,9,30);
        var time = new DateTimeOffset(date,new TimeOnly(12,0),TimeSpan.Zero);
        var raw = new FuturesEodObservationReadModel
        {
            MarketSeriesIdentity = series, ContractId = "ESZ26", ValueDate = date,
            SessionStartUtc = time.AddHours(-12), SessionEndUtc = time.AddHours(9), Open = 6000.125m,
            High = 6010.25m, Low = 5990.75m, Close = 6001.125m, Volume = 100m, TradeCount = 10,
            PriceVolumeSum = 600112.5m, ObservationId = new(Guid.NewGuid()), FirstSourceSequence = 1,
            LastSourceSequence = 10, FirstMarketEventUtc = time, LastMarketEventUtc = time.AddHours(1),
            IsComplete = true, IsValid = true
        };
        Assert.True(await db.TryWriteRawEodAsync(raw,CancellationToken.None));
        Assert.False(await db.TryWriteRawEodAsync(raw with { Close = 9999m },CancellationToken.None));
        (await db.GetRawEodAsync(series,date,CancellationToken.None)).Should().BeEquivalentTo(raw);
        var next = raw with { ValueDate = date.AddDays(1), ObservationId = new(Guid.NewGuid()), Close = 6002.25m };
        Assert.True(await db.TryWriteRawEodAsync(next,CancellationToken.None));
        var range = await db.GetRawEodRangeAsync(series,date,date.AddDays(1),CancellationToken.None);
        Assert.Equal(new[] { date,date.AddDays(1) },range.Select(row => row.ValueDate));
        Assert.Empty(await db.GetRawEodRangeAsync(series,date.AddDays(2),date.AddDays(3),CancellationToken.None));
        var bar = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series, ContractId = raw.ContractId, ValueDate = date, TimeFrame = TimeFrameType.Daily,
            IntervalStartUtc = raw.SessionStartUtc, IntervalEndUtc = raw.SessionEndUtc, ObservationId = raw.ObservationId,
            Open = raw.Open, High = raw.High, Low = raw.Low, Close = raw.Close, Volume = raw.Volume, TradeCount = raw.TradeCount,
            PriceVolumeSum = raw.PriceVolumeSum, FirstSourceSequence = 1, LastSourceSequence = 10,
            FirstMarketEventUtc = raw.FirstMarketEventUtc, LastMarketEventUtc = raw.LastMarketEventUtc,
            CalculatedAtUtc = time.AddHours(10), CalculationVersion = "fixture-v1", IsComplete = true, IsValid = true
        };
        Assert.True(await db.TryWriteObservationAsync(bar,CancellationToken.None));
        Assert.False(await db.TryWriteObservationAsync(bar with { Close = 9999m },CancellationToken.None));
        Assert.True(await db.TryWriteObservationAsync(bar with { ObservationId = new(Guid.NewGuid()) },CancellationToken.None));
    }

    static async Task VerifyDownloadAndFeedStateAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var feed = new TradeLiveFeedReadModel(1,1,TradeLiveFeedStateType.Unknown);
        await db.InsertTradeLiveFeedAsync(feed);
        await db.InsertTradeLiveFeedAsync(feed);
        (await db.GetTradeLiveFeedAsync(1,1)).Should().BeEquivalentTo(feed);
        Assert.Null(await db.GetTradeLiveFeedAsync(1,2));
        await db.DeleteTradeLiveFeedAsync(1,1);
        Assert.Null(await db.GetTradeLiveFeedAsync(1,1));
        var requested = date.ToDateTime(new TimeOnly(12,0),DateTimeKind.Utc);
        var outcome = new MarketDataDownloadOutcome
        {
            Dataset = MarketDataDownloadDataset.TreasuryCurve, Scope = "US", ValueDate = date,
            ImportCommandId = Guid.NewGuid(), SourceTerminalEventId = Guid.NewGuid(), RequestedAtUtc = requested,
            StartedAtUtc = requested.AddSeconds(1), FinishedAtUtc = requested.AddSeconds(2),
            Status = MarketDataDownloadStatus.Completed, DownloadedRecordCount = 1, PersistedRecordCount = 1, ElapsedMilliseconds = 1000
        };
        var partition = new MarketDataDownloadPartition(outcome.Dataset,outcome.Provider,outcome.Scope,date);
        for (var i=0;i<3;i++)
        {
            var row = outcome with { ImportCommandId = Guid.NewGuid(), RequestedAtUtc = requested.AddSeconds(i),
                StartedAtUtc = requested.AddSeconds(i+1), FinishedAtUtc = requested.AddSeconds(i+2) };
            await db.InsertMarketDataDownloadLogAsync(row,MarketDataDownloadOutcome.LoggingCommandId(row.ImportCommandId),row.ComputeHash());
            await db.InsertMarketDataDownloadLogAsync(row,MarketDataDownloadOutcome.LoggingCommandId(row.ImportCommandId),row.ComputeHash());
            var exact = await db.GetMarketDataDownloadLogAsync(partition,new(row.RequestedAtUtc,row.ImportCommandId));
            exact.Attempt!.Outcome.Should().BeEquivalentTo(row);
        }
        var first = await db.GetMarketDataDownloadHistoryAsync(partition,2);
        Assert.Equal(new[] {requested.AddSeconds(2),requested.AddSeconds(1)},first.Attempts.Select(row => row.Outcome.RequestedAtUtc));
        Assert.NotNull(first.Continuation);
        var last = await db.GetMarketDataDownloadHistoryAsync(partition,2,first.Continuation);
        Assert.Single(last.Attempts);
        Assert.Equal(requested,last.Attempts[0].Outcome.RequestedAtUtc);
        Assert.Null(last.Continuation);
        Assert.True((await db.GetMarketDataDownloadStatusAsync(partition)).CompletionConfirmed);
    }

    static async Task VerifyOptionChainAndReturnsAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date)
    {
        var tick = SampleData.FuturesOptionTickData with { ValueDate = date };
        var expiry = new DateOnly(2018,5,11);
        await db.UpsertFuturesOptionChainQuoteDataAsync("ES",tick);
        await db.UpsertFuturesOptionChainQuoteDataAsync("ES",tick);
        var quotes = await db.GetFuturesOptionChainQuoteDataAsync("ES",expiry,date);
        quotes.Should().ContainSingle().Which.Should().BeEquivalentTo(tick);
        var enriched = tick with { Delta = 0.125, Gamma = 0.0025, UnderlyingPrice = 6000.125, ImpliedVolatility = 0.225 };
        await db.UpdateFuturesOptionChainQuoteGreeksAsync("ES",expiry,new[] { enriched });
        var changed = (await db.GetFuturesOptionChainQuoteDataAsync("ES",expiry,date)).Single();
        changed.Should().BeEquivalentTo(enriched);
        await db.UpdateFuturesOptionChainQuoteGreeksAsync("ES",expiry,new[] { enriched with { TickId = tick.TickId-1, Delta = 0.99 } });
        Assert.Equal(0.125,(await db.GetFuturesOptionChainQuoteDataAsync("ES",expiry,date)).Single().Delta);
        Assert.Empty(await db.GetFuturesOptionChainQuoteDataAsync("ES",expiry,date.AddDays(1)));
        var rate = new RateOfReturnReadModel { Symbol = "RATE", ValueDate = date, RateOfReturn = 0.0125 };
        await db.InsertRateOfReturnAsync(rate);
        await db.InsertRateOfReturnAsync(rate with { ValueDate = date.AddDays(1), RateOfReturn = 0.025 });
        Assert.Equal(0.025,(await db.GetLastRateOfReturnAsync("RATE"))!.RateOfReturn);
        await db.InsertRateOfReturnAsync(rate with { ValueDate = date.AddDays(1), RateOfReturn = 0.0325 });
        Assert.Equal(0.0325,(await db.GetLastRateOfReturnAsync("RATE",CancellationToken.None))!.RateOfReturn);
        await db.DeleteRateOfReturnAsync("RATE",date.AddDays(1));
        Assert.Equal(0.0125,(await db.GetLastRateOfReturnAsync("RATE"))!.RateOfReturn);
        await db.DeleteRateOfReturnAsync("RATE",date);
        Assert.Null(await db.GetLastRateOfReturnAsync("RATE"));
    }

    static async Task VerifyVixEodAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date, string connectionString)
    {
        var row = new VixFuturesEodDataReadModel("VXA",date,20.125m,21.25m,19.75m,20.5m,100);
        var other = row with { ContractId = "VXB", ClosePrice = 21.5m };
        await db.PersistRealtimeVixEodBatchAsync(new[] {other,row});
        await db.PersistRealtimeVixEodBatchAsync(new[] {row});
        var byDate = await db.GetVixFuturesEodDataByValueDateAsync(date);
        Assert.Equal(new[] {"VXA","VXB"},byDate.Select(x => x.ContractId));
        (await db.GetVixFuturesEodDataAsync("VXA",date)).Should().BeEquivalentTo(row);
        Assert.Empty(await db.GetVixFuturesEodDataByValueDateAsync(date.AddDays(-1)));
        await db.PersistRealtimeVixEodBatchAsync(new[] { row with { ValueDate = date.AddDays(-1), ClosePrice = 19m } });
        Assert.Equal(19m,(await db.GetLastVixFuturesEodDataAsync("VXA",date.AddDays(-1)))!.ClosePrice);
        var tick = SampleData.FuturesTickData with { ContractId = "VX-TICK", ValueDate = date, Price = 20.125m, Size = 2 };
        await Task.WhenAll(Enumerable.Range(0,4).Select(_ => db.InsertVixFuturesEodDataAsync(tick)));
        Assert.Equal(8,(await db.GetVixFuturesEodDataAsync(tick.ContractId,date))!.Volume);
        var stats = new FuturesSessionStatisticsSnapshot(tick.ContractId,date,19m,22m,18m,1,1,50,FuturesSessionVolumeQuality.ObservedComplete);
        await db.InsertVixFuturesEodDataAsync(tick,stats);
        var marked = (await db.GetVixFuturesEodDataAsync(tick.ContractId,date))!;
        Assert.Equal(50,marked.Volume);
        Assert.Equal(19m,marked.OpenPrice);
        Assert.Equal(22m,marked.HighPrice);
        Assert.Equal(18m,marked.LowPrice);
        await Assert.ThrowsAsync<ArgumentException>(() => db.InsertVixFuturesEodDataAsync(tick,stats with { ContractId = "WRONG" }));
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var constraint = new NpgsqlCommand("ALTER TABLE market_data.vix_futures_eod_data ADD CONSTRAINT reject_vix_test CHECK(closePrice<>999);",connection))
            await constraint.ExecuteNonQueryAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => db.PersistRealtimeVixEodBatchAsync(new[] { row with { ClosePrice = 25m },other with { ClosePrice = 999m } }));
        Assert.Equal(20.5m,(await db.GetVixFuturesEodDataAsync("VXA",date))!.ClosePrice);
        await db.DeleteVixFuturesEodDataAsync("VXA",date);
        Assert.Null(await db.GetVixFuturesEodDataAsync("VXA",date));
        Assert.Equal(19m,(await db.GetLastVixFuturesEodDataAsync("VXA",date))!.ClosePrice);
    }

    static async Task VerifyTreasuryCurveAsync(Storage.MarketDataDb.MarketDataDbContext db, DateOnly date, string connectionString)
    {
        var row = SampleData.YieldCurveRate with { ValueDate = date };
        var next = row with { ValueDate = new DateOnly(2027,1,1), TenYear = 0.0425 };
        await db.InsertYieldCurveRatesAsync(new[] {row,next});
        (await db.GetYieldCurveRateAsync(date)).Should().BeEquivalentTo(row);
        (await db.GetLastYieldCurveRateAsync(CancellationToken.None)).Should().BeEquivalentTo(next);
        Assert.Equal(new[] {2027,2026},await db.GetYieldCurveRateYearsAsync());
        Assert.Equal(new[] {next.ValueDate,date},(await db.GetYieldCurveRatesAsync(date,next.ValueDate)).Select(x => x.ValueDate));
        var command = Guid.NewGuid();
        var owned = row with { ValueDate = date.AddDays(1), TenYear = 0.05 };
        await db.InsertYieldCurveRatesAsync(new[] {owned},ImportDuplicatePolicy.Reject,command);
        await db.InsertYieldCurveRatesAsync(new[] {owned},ImportDuplicatePolicy.Reject,command);
        await Assert.ThrowsAsync<MarketDataImportDuplicateException>(() => db.InsertYieldCurveRatesAsync(new[] {owned},ImportDuplicatePolicy.Reject,Guid.NewGuid()));
        await Assert.ThrowsAsync<MarketDataImportDuplicateException>(() => db.InsertYieldCurveRatesAsync(new[] { row with { ValueDate = date.AddDays(-1) },owned },ImportDuplicatePolicy.Reject,Guid.NewGuid()));
        Assert.Null(await db.GetYieldCurveRateAsync(date.AddDays(-1)));
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync();
        await using (var constraint = new NpgsqlCommand("ALTER TABLE market_data.yield_curve_rate_by_date ADD CONSTRAINT reject_curve_test CHECK(oneMonth<>999);",connection))
            await constraint.ExecuteNonQueryAsync();
        var rejected = row with { ValueDate = date.AddDays(2), OneMonth = 999 };
        await Assert.ThrowsAnyAsync<Exception>(() => db.InsertYieldCurveRatesAsync(new[] {rejected},ImportDuplicatePolicy.Reject,Guid.NewGuid()));
        Assert.Null(await db.GetYieldCurveRateAsync(rejected.ValueDate));
        await using var verify = new NpgsqlCommand("SELECT (SELECT count(*) FROM market_data.yield_curve_rates WHERE valueDate=@date)+(SELECT count(*) FROM market_data.market_data_import_ownership WHERE dataset='treasury-curve' AND logicalKey=@key);",connection);
        verify.Parameters.AddWithValue("date",rejected.ValueDate);
        verify.Parameters.AddWithValue("key",rejected.ValueDate.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L,(long)(await verify.ExecuteScalarAsync())!);
        await using(var remove = new NpgsqlCommand("DELETE FROM market_data.yield_curve_rate_by_date WHERE valueDate=$1;",connection))
        {
            remove.Parameters.AddWithValue(date);
            await remove.ExecuteNonQueryAsync();
        }
        await using(var removeYears = new NpgsqlCommand("DELETE FROM market_data.yield_curve_rate_year WHERE lookupId=1",connection))
            await removeYears.ExecuteNonQueryAsync();
        var rebuilt = await db.BackfillFmpQueryProjectionsAsync();
        Assert.Equal(3L,rebuilt.YieldCurveRowsSource);
        Assert.Equal(rebuilt.YieldCurveSourceFingerprint,rebuilt.YieldCurveProjectedFingerprint);
        Assert.Equal(new[] {2027,2026},await db.GetYieldCurveRateYearsAsync());
        await using(var guard = new NpgsqlCommand("ALTER TABLE market_data.yield_curve_rate_year ADD CONSTRAINT treasury_rebuild_rollback CHECK(rateYear<>2027) NOT VALID",connection))
            await guard.ExecuteNonQueryAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.BackfillFmpQueryProjectionsAsync());
        (await db.GetYieldCurveRateAsync(date)).Should().BeEquivalentTo(row);
        Assert.Equal(new[] {2027,2026},await db.GetYieldCurveRateYearsAsync());
        await db.DeleteYieldCurveRateAsync(date);
        Assert.Null(await db.GetYieldCurveRateAsync(date));
        (await db.GetLastYieldCurveRateAsync()).Should().BeEquivalentTo(next);
    }

}
