using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesRsiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ShortIndicatorRestartTests
{
    /// <summary>Already initialized short frames repair later gaps from original bars and retain retry idempotency.</summary>
    [Theory]
    [InlineData(TimeFrameType.FifteenSeconds, false)]
    [InlineData(TimeFrameType.OneMinute, false)]
    [InlineData(TimeFrameType.FifteenSeconds, true)]
    [InlineData(TimeFrameType.OneMinute, true)]
    public async Task Warm_cached_frame_reacquires_original_history_after_a_gap_then_stops_replaying(TimeFrameType frame, bool initiallyUnavailable)
    {
        var at = new DateTimeOffset(2026, 11, 9, 10, 0, 10, TimeSpan.Zero);
        var clock = new MutableClock(at);
        var date = new DateOnly(2026, 11, 9);
        var contract = "ES-GAP-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var databases = Substitute.For<IDbContextFactory>();
        var db = databases.MarketDataDb;
        var actors = Substitute.For<IActorService>();
        var market = Substitute.For<IMarketDataApi>();
        var calendar = new CmeFuturesMarketSessionCalendar();
        var accepted = new ServiceOk<Guid>(Guid.NewGuid());
        DateTimeOffset committed = at.AddSeconds(-10);
        Bind(committed, includeBand: true);
        actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(Arg.Any<StartFuturesRsiSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(Arg.Any<StartFuturesAdxSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(Arg.Any<StartFuturesMacdSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(Arg.Any<StartFuturesAtrSignalCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var command = call.Arg<StartFuturesAtrSignalCommand>();
            if (command.HistoricalSeed is { Length: > 0 } bars) { committed = bars[^1].LastMarketEventUtc; Bind(committed, includeBand: false); }
            return accepted;
        });
        actors.RequestAsync<SeedFuturesEmaSignalsCommand, FuturesTradeSessionBarEntityId>(Arg.Any<SeedFuturesEmaSignalsCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<SeedFuturesBbSignalsCommand, FuturesTradeSessionBarEntityId>(Arg.Any<SeedFuturesBbSignalsCommand>(), Arg.Any<CancellationToken>()).Returns(_ => { Bind(committed, includeBand: true); return accepted; });
        databases.TradeTickQuoteDb.GetRawFuturesTradeWindowAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TomasAI.IFM.Application.Storage.TradeTickQuoteDb.BootstrapTradeRow>>([]));
        var reader = new StoredSessionIndicatorSeedReader(databases, calendar, clock, NullLogger<StoredSessionIndicatorSeedReader>.Instance, market);
        var warmup = new StoredSessionIndicatorWarmup(reader, actors, market, databases, calendar, clock, NullLogger<StoredSessionIndicatorWarmup>.Instance);
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        clock.Now = at.AddMinutes(5);
        var original = RsiHistoricalSeedWindowModel.Create(frame, 48, clock.Now, calendar).Select((window, index) =>
        {
            var sourceTime = window.StartUtc.AddSeconds(1);
            var ns = (sourceTime.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
            return StoredSessionIndicatorSeedReader.Aggregate(contract, frame, window, clock.Now,
                [new(window.ValueDate, 100m, 1, index + 1, ns, ns, (short)'T', 0, 42, 1)])!;
        }).ToArray();
        if (initiallyUnavailable)
        {
            market.ReadIndicatorSeedBarsAsync(contract, frame, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Array.Empty<FuturesTradeSessionBarReadModel>()), Task.FromResult(original));
            Assert.False(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        }
        else market.ReadIndicatorSeedBarsAsync(contract, frame, Arg.Any<CancellationToken>()).Returns(Task.FromResult(original));
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        await market.Received(initiallyUnavailable ? 2 : 1).ReadIndicatorSeedBarsAsync(contract, frame, Arg.Any<CancellationToken>());
        await databases.TradeTickQuoteDb.DidNotReceive().GetRawFuturesTradeWindowAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<TimeOnly>(), Arg.Any<TimeOnly>(), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(
            Arg.Is<StartFuturesAtrSignalCommand>(c => c.HistoricalSeed != null && c.HistoricalSeed.SequenceEqual(original)), Arg.Any<CancellationToken>());
        Assert.Equal(original[^1].LastMarketEventUtc, committed);

        void Bind(DateTimeOffset source, bool includeBand)
        {
            var metadata = new MarketAnalyticsSignalMetadata { ContractId = contract, ValueDate = date, MarketDataAsOfUtc = source,
                CalculatedAtUtc = clock.Now, IsValid = true, SchemaVersion = 1, CalculationVersion = "test",
                SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, frame, "test") };
            db.GetLastFuturesAdxSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>()).Returns(new FuturesAdxSignalReadModel { Metadata = metadata, IsWarm = true });
            db.GetLastFuturesAtrSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>()).Returns(new FuturesAtrSignalReadModel { Metadata = metadata, IsWarm = true, AtrRatio = 1 });
            db.GetLastFuturesMacdSignalAsync(contract, date, frame, 9, 12, 26, Arg.Any<CancellationToken>()).Returns(new FuturesMacdSignalReadModel { Metadata = metadata, IsWarm = true });
            foreach (var period in new[] { 13, 14 })
                db.GetLastFuturesRsiSignalAsync(contract, date, frame, period, Arg.Any<CancellationToken>()).Returns(new FuturesRsiSignalReadModel { Metadata = metadata, IsWarm = true, PeriodLength = period });
            db.GetLastFuturesTdiSignalAsync(contract, date, frame, FuturesTdiConfiguration.StandardConfigurationId, Arg.Any<CancellationToken>())
                .Returns(new FuturesTdiSignalReadModel { ContractId = contract, ValueDate = date, TimePeriod = frame, SchemaVersion = 2, SourceEventTimestamp = source.UtcDateTime });
            if (includeBand) db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, date, frame, Arg.Any<CancellationToken>())
                .Returns(new FuturesBbSignalReadModel { Metadata = metadata, IsWarm = true, Ema20Center = 100, Width20 = 2, Width20Ratio = 1, Position20 = .5m });
        }
    }

    sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData(TimeFrameType.FifteenSeconds)]
    [InlineData(TimeFrameType.OneMinute)]
    public async Task Warm_restart_restores_every_dependency_and_attaches_live_processors_without_replay(TimeFrameType frame)
    {
        var at = new DateTimeOffset(2026, 11, 9, 10, 0, 10, TimeSpan.Zero);
        var date = new DateOnly(2026, 11, 9);
        var contract = "ES-RESTART-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var metadata = new MarketAnalyticsSignalMetadata
        {
            ContractId = contract, ValueDate = date, MarketDataAsOfUtc = at.AddSeconds(-10), CalculatedAtUtc = at,
            IsValid = true, SchemaVersion = 1, CalculationVersion = "test",
            SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, frame, "test")
        };
        var databases = Substitute.For<IDbContextFactory>();
        var db = databases.MarketDataDb;
        db.GetLastFuturesAdxSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>())
            .Returns(new FuturesAdxSignalReadModel { Metadata = metadata, IsWarm = true });
        db.GetLastFuturesAtrSignalAsync(contract, date, frame, 14, Arg.Any<CancellationToken>())
            .Returns(new FuturesAtrSignalReadModel { Metadata = metadata, IsWarm = true, AtrRatio = 1 });
        db.GetLastFuturesMacdSignalAsync(contract, date, frame, 9, 12, 26, Arg.Any<CancellationToken>())
            .Returns(new FuturesMacdSignalReadModel { Metadata = metadata, IsWarm = true });
        foreach (var period in new[] { 13, 14 })
            db.GetLastFuturesRsiSignalAsync(contract, date, frame, period, Arg.Any<CancellationToken>())
                .Returns(new FuturesRsiSignalReadModel { Metadata = metadata, IsWarm = true, PeriodLength = period });
        db.GetLastFuturesTdiSignalAsync(contract, date, frame, FuturesTdiConfiguration.StandardConfigurationId, Arg.Any<CancellationToken>())
            .Returns(new FuturesTdiSignalReadModel { ContractId = contract, ValueDate = date, TimePeriod = frame,
                Timestamp = TimeOnly.FromDateTime(at.UtcDateTime), SchemaVersion = 2, SourceEventTimestamp = at.UtcDateTime });
        db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, date, frame, Arg.Any<CancellationToken>())
            .Returns(new FuturesBbSignalReadModel { Metadata = metadata, IsWarm = true, Width20 = 2, Width20Ratio = 1, Position20 = .5m });
        var actors = Substitute.For<IActorService>();
        var accepted = new ServiceOk<Guid>(Guid.NewGuid());
        actors.RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(Arg.Any<StartFuturesRsiSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(Arg.Any<StartFuturesAdxSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(Arg.Any<StartFuturesAtrSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        actors.RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(Arg.Any<StartFuturesMacdSignalCommand>(), Arg.Any<CancellationToken>()).Returns(accepted);
        var market = Substitute.For<IMarketDataApi>();
        var warmup = new StoredSessionIndicatorWarmup(null!, actors, market, databases,
            new CmeFuturesMarketSessionCalendar(), new Clock(at), NullLogger<StoredSessionIndicatorWarmup>.Instance);

        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        Assert.True(await warmup.WarmShortFrameAsync(contract, date, frame, default));
        await actors.Received(2).RequestAsync<StartFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
            Arg.Is<StartFuturesRsiSignalCommand>(c => c.HistoricalSeed == null), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(Arg.Any<StartFuturesAdxSignalCommand>(), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(Arg.Any<StartFuturesAtrSignalCommand>(), Arg.Any<CancellationToken>());
        await actors.Received(1).RequestAsync<StartFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(Arg.Any<StartFuturesMacdSignalCommand>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accepted_EMA_seed_waits_for_matching_durable_Bollinger_projection()
    {
        var db = Substitute.For<IDbContextFactory>();
        var at = DateTimeOffset.UtcNow;
        var series = MarketSeriesIdentity.ForContract("ES-DEFERRED");
        var published = new TaskCompletionSource<FuturesBbSignalReadModel?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = 0;
        db.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, new(2026, 11, 9), TimeFrameType.OneMinute, Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref queries); queried.TrySetResult();
                return published.Task.IsCompleted ? published.Task : Task.FromResult<FuturesBbSignalReadModel?>(null); });
        var pending = StoredSessionIndicatorWarmup.AwaitBollingerAsync(db, "ES-DEFERRED", new(2026, 11, 9), TimeFrameType.OneMinute, at, default);
        await queried.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted);
        published.SetResult(new() { Metadata = new() { ContractId = "ES-DEFERRED", SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, TimeFrameType.OneMinute, "test"),
            MarketDataAsOfUtc = at, IsValid = true, SchemaVersion = 1, CalculationVersion = "test" },
            IsWarm = true, Width20 = 2, Width20Ratio = 1, Position20 = .5m });
        Assert.True(await pending);
        Assert.True(queries >= 2);
    }

    [Fact]
    public async Task Missing_Bollinger_projection_remains_cancellable_and_cannot_report_ready()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StoredSessionIndicatorWarmup.AwaitBollingerAsync(
            Substitute.For<IDbContextFactory>(), "ES-MISSING", new(2026, 11, 9), TimeFrameType.OneMinute,
            DateTimeOffset.UtcNow, timeout.Token));
    }

    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
