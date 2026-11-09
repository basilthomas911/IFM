using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

/// <summary>Qualifies required Regime Discovery cache evidence after restart without another EMA-generated event.</summary>
public sealed class StoredBollingerRestoreTests
{
    [Theory]
    [InlineData(TimeFrameType.FifteenSeconds, false)]
    [InlineData(TimeFrameType.OneMinute, false)]
    [InlineData(TimeFrameType.FifteenSeconds, true)]
    [InlineData(TimeFrameType.OneMinute, true)]
    public async Task Fast_frame_startup_retries_only_unfinished_accumulators(TimeFrameType frame, bool failMacdOnce)
    {
        const string contract = "ES20261218";
        var date = new DateOnly(2026, 11, 6);
        var now = new DateTimeOffset(2026, 11, 6, 0, 15, 0, TimeSpan.Zero);
        var calendar = new CmeFuturesMarketSessionCalendar();
        var clock = new FixedClock(now);
        var series = MarketSeriesIdentity.ForContract(contract);
        var windows = RsiHistoricalSeedWindowModel.Create(frame, 48, now, calendar);
        var bars = windows.Select((window, index) => new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series, ContractId = contract, TimeFrame = frame, ValueDate = window.ValueDate,
            ObservationId = FuturesTradeSessionBarId.Create(series, frame, window.EndUtc, index + 1),
            IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
            Open = 5000, High = 5001, Low = 4999, Close = 5000, Volume = 1, TradeCount = 1, PriceVolumeSum = 5000,
            FirstSourceSequence = index + 1, LastSourceSequence = index + 1,
            FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddMilliseconds(-1),
            CalculatedAtUtc = now, IsComplete = true, IsValid = true, SchemaVersion = 1, CalculationVersion = "test",
            CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        }).ToArray();
        var databases = Substitute.For<IDbContextFactory>();
        var db = databases.MarketDataDb;
        db.GetStoredTradeSessionBarsAsync(contract, frame, Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset[]>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>(
                bars.Where(bar => call.Arg<DateTimeOffset[]>().Contains(bar.IntervalEndUtc)).ToArray()));
        db.GetLastFuturesAtrSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>())
            .Returns(new FuturesAtrSignalReadModel
            {
                ContractId = contract, TimePeriod = frame, ValueDate = date,
                Timestamp = TimeOnly.FromDateTime(now.UtcDateTime),
                IsWarm = true, AtrValue = 2, AtrBaseline = 2, AtrRatio = 1
            });
        var liveSeries = StoredSessionIndicatorWarmup.LiveIndicatorSeries(contract);
        db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(liveSeries, date, frame, Arg.Any<CancellationToken>())
            .Returns(new FuturesBbSignalReadModel
            {
                Metadata = new() { ContractId = contract, SignalKey = new(liveSeries, MarketAnalyticsSignalKind.BollingerBand, frame, "bb"),
                    ValueDate = date, MarketDataAsOfUtc = now, CalculatedAtUtc = now, SourceSequence = 48,
                    IsValid = true, SchemaVersion = 1, CalculationVersion = "test" },
                IsWarm = true, Width20 = 80, Width20Ratio = 1.25m, Position20 = 0.6m
            });
        var actors = Substitute.For<IActorService>();
        var accepted = new ServiceOk<Guid>(Guid.NewGuid());
        actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(Arg.Any<StartFuturesRsiSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(Arg.Any<StartFuturesAdxSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(Arg.Any<StartFuturesAtrSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        var macdCalls = 0;
        actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(Arg.Any<StartFuturesMacdSignalCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref macdCalls) == 1 && failMacdOnce) throw new IOException("injected MACD failure");
                // Model the read models committed by the accepted initialization.
                // A cached success is now requalified against these outputs.
                var metadata = new MarketAnalyticsSignalMetadata { ContractId = contract, ValueDate = date,
                    SignalKey = new(liveSeries, MarketAnalyticsSignalKind.BollingerBand, frame, "test"),
                    MarketDataAsOfUtc = bars[^1].LastMarketEventUtc, CalculatedAtUtc = now, IsValid = true,
                    SchemaVersion = 1, CalculationVersion = "test" };
                db.GetLastFuturesAdxSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>())
                    .Returns(new FuturesAdxSignalReadModel { Metadata = metadata, IsWarm = true });
                db.GetLastFuturesAtrSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>())
                    .Returns(new FuturesAtrSignalReadModel { Metadata = metadata, IsWarm = true, AtrValue = 2, AtrBaseline = 2, AtrRatio = 1 });
                db.GetLastFuturesMacdSignalAsync(contract, date, frame, 9, 12, 26, Arg.Any<CancellationToken>())
                    .Returns(new FuturesMacdSignalReadModel { Metadata = metadata, IsWarm = true });
                foreach (var period in new[] { 13, 14 })
                    db.GetLastFuturesRsiSignalAsync(contract, date, frame, period, Arg.Any<CancellationToken>())
                        .Returns(new FuturesRsiSignalReadModel { Metadata = metadata, IsWarm = true, PeriodLength = period });
                db.GetLastFuturesTdiSignalAsync(contract, date, frame, FuturesTdiConfiguration.StandardConfigurationId, Arg.Any<CancellationToken>())
                    .Returns(new FuturesTdiSignalReadModel { ContractId = contract, ValueDate = date, TimePeriod = frame,
                        SchemaVersion = 2, SourceEventTimestamp = bars[^1].LastMarketEventUtc.UtcDateTime });
                return accepted;
            });
        var market = Substitute.For<IMarketDataApi>();
        var reader = new StoredSessionIndicatorSeedReader(databases, calendar, clock, NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        var warmup = new StoredSessionIndicatorWarmup(reader, actors, market, databases, calendar, clock,
            NullLogger<StoredSessionIndicatorWarmup>.Instance);

        Assert.Equal(!failMacdOnce, await warmup.WarmShortFrameAsync(contract, date, frame, default));
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));

        foreach (var period in new[] { 13, 14 })
            await actors.Received(1).RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Is<StartFuturesRsiSignalCommand>(command => command.EntityId.PeriodLength == period && command.HistoricalSeed!.Observations.SequenceEqual(bars)), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(
            Arg.Is<StartFuturesAdxSignalCommand>(command => command.HistoricalSeed!.SequenceEqual(bars)), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(
            Arg.Is<StartFuturesAtrSignalCommand>(command => command.HistoricalSeed!.SequenceEqual(bars)), Arg.Any<CancellationToken>());
        await actors.Received(failMacdOnce ? 2 : 1).RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(
            Arg.Is<StartFuturesMacdSignalCommand>(command => command.HistoricalSeed!.SequenceEqual(bars)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Warm_contract_seed_does_not_hide_cold_live_continuation_stream()
    {
        var databases = Substitute.For<IDbContextFactory>();
        const string contract = "ES20261218";
        var date = new DateOnly(2026, 11, 9);
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(MarketSeriesIdentity.ForContract(contract), date, TimeFrameType.FourHours, Arg.Any<CancellationToken>())
            .Returns(new FuturesBbSignalReadModel { IsWarm = true, Width20 = 2, Width20Ratio = 1, Position20 = .5m });
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(StoredSessionIndicatorWarmup.LiveIndicatorSeries(contract), date, TimeFrameType.FourHours, Arg.Any<CancellationToken>())
            .Returns(new FuturesBbSignalReadModel { IsWarm = false });
        Assert.False(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, TimeFrameType.FourHours, default));
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Four_hour_restore_starts_while_one_hour_restore_is_blocked()
    {
        var market = Substitute.For<IMarketDataApi>();
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>())
            .Returns(call => { call[1] = new FuturesContractV3ReadModel { ContractId = "ES20261218" }; return true; });
        var databases = Substitute.For<IDbContextFactory>();
        var oneHour = new TaskCompletionSource<FuturesAdxSignalReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var atr = new TaskCompletionSource<FuturesAtrSignalReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var macd = new TaskCompletionSource<FuturesMacdSignalReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var rsi = new TaskCompletionSource<FuturesRsiSignalReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fourHours = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var db = databases.MarketDataDb;
        db.GetLastFuturesAdxSignalAsync("ES20261218", Arg.Any<DateOnly>(),
            TimeFrameType.OneHour, 14, Arg.Any<CancellationToken>()).Returns(oneHour.Task);
        db.GetLastFuturesAtrSignalAsync("ES20261218", Arg.Any<DateOnly>(), TimeFrameType.OneHour, 14, Arg.Any<CancellationToken>()).Returns(atr.Task);
        db.GetLastFuturesMacdSignalAsync("ES20261218", Arg.Any<DateOnly>(), TimeFrameType.OneHour, 9, 12, 26, Arg.Any<CancellationToken>()).Returns(macd.Task);
        db.GetLastFuturesRsiSignalAsync("ES20261218", Arg.Any<DateOnly>(), TimeFrameType.OneHour, 14, Arg.Any<CancellationToken>()).Returns(rsi.Task);
        db.GetLastFuturesAdxSignalAsync("ES20261218", Arg.Any<DateOnly>(),
            TimeFrameType.FourHours, 14, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fourHours.TrySetResult();
            return Task.FromException<FuturesAdxSignalReadModel?>(
                new InvalidOperationException("Simulated restore failure."));
        });
        var warmup = new StoredSessionIndicatorWarmup(null!, Substitute.For<IActorService>(), market, databases,
            new TomasAI.IFM.Application.MarketData.Databento.Historical.CmeFuturesMarketSessionCalendar(), TimeProvider.System,
            NullLogger<StoredSessionIndicatorWarmup>.Instance);
        var attempt = warmup.WarmAsync(new DateOnly(2026, 11, 4), default);
        try
        {
            await fourHours.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(attempt.IsCompleted);
        }
        finally
        {
            oneHour.TrySetException(new InvalidOperationException("Simulated restore failure."));
            atr.TrySetException(new InvalidOperationException("Simulated restore failure."));
            macd.TrySetException(new InvalidOperationException("Simulated restore failure."));
            rsi.TrySetException(new InvalidOperationException("Simulated restore failure."));
        }
        Assert.False(await attempt);
    }
    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    public async Task Committed_warm_bands_restore_all_three_required_metrics_without_new_market_data(TimeFrameType frame)
    {
        var databases = Substitute.For<IDbContextFactory>();
        var contract = "ES-RESTORE-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var at = DateTimeOffset.UtcNow.AddMinutes(-1);
        var date = DateOnly.FromDateTime(at.UtcDateTime);
        var stored = new FuturesBbSignalReadModel
        {
            Metadata = new() { ContractId = contract, SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, frame, "bb"),
                ValueDate = date, MarketDataAsOfUtc = at, CalculatedAtUtc = at, SourceSequence = 42,
                IsValid = true, SchemaVersion = 1, CalculationVersion = "1" },
            IsWarm = true, Width20 = 80, Width20Ratio = 1.25m, Position20 = 0.6m
        };
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, date, frame, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesBbSignalReadModel?>(stored));
        Assert.True(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, frame, default));
        Assert.False(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, frame, default, at.AddMinutes(1)));
        RegimeDiscoverySignalMetric[] metrics = [RegimeDiscoverySignalMetric.BollingerWidth, RegimeDiscoverySignalMetric.BollingerWidthRatio, RegimeDiscoverySignalMetric.BollingerPosition];
        var snapshot = await new RegimeDiscoveryMarketSignalSnapshotProvider().CaptureAsync(new()
        {
            MarketSeriesIdentity = series, TargetHorizon = TimeFrameType.Daily, SupportedSchemaVersions = [1], ApprovedCalculationVersions = ["1"],
            Requirements = metrics.Select(metric => new RegimeDiscoverySignalRequirement
            { Metric = metric, TimeFrame = frame, IsRequired = true, CalculationConfigurationId = $"{metric}.v1", MaximumAgeSeconds = 3600, Weight = 1 }).ToArray()
        });
        Assert.True(snapshot.IsSuccess);
        Assert.Equal(3, snapshot.Snapshot!.Observations.Length);
        Assert.All(snapshot.Snapshot.Observations, observation => Assert.Equal(42, observation.SourceSequence));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_cold_bands_keep_initialization_retryable(bool cold)
    {
        var databases = Substitute.For<IDbContextFactory>();
        var contract = "ES-COLD-" + Guid.NewGuid().ToString("N");
        var date = new DateOnly(2026, 11, 4);
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(MarketSeriesIdentity.ForContract(contract), date, TimeFrameType.OneHour, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FuturesBbSignalReadModel?>(cold ? new() { IsWarm = false } : null));
        Assert.False(await StoredSessionIndicatorWarmup.RestoreBollingerAsync(databases, contract, date, TimeFrameType.OneHour, default));
    }
}
