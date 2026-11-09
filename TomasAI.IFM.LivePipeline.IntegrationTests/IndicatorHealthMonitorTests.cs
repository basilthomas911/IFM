using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class IndicatorHealthMonitorTests
{
    [Fact]
    public async Task Incident_storage_failure_is_visible_without_stopping_independent_monitoring()
    {
        var blocked = Path.GetTempFileName();
        try
        {
            var clock = new ManualClock();
            var reader = Substitute.For<IIndicatorHealthReader>();
            reader.ReadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<IndicatorHealthEntry>());
            var console = Substitute.For<IStatusConsoleEventProducer>();
            console.IsRunning.Returns(true);
            using var monitor = Create(reader, console, clock, blocked);
            await monitor.StartAsync(default);
            await Until(() => Task.FromResult(reader.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "ReadAsync")));
            var health = await Health(monitor);
            Assert.Equal(HealthStatus.Unhealthy, health.Status);
            Assert.Contains("persist", health.Description);
            await monitor.StopAsync(default);
        }
        finally { File.Delete(blocked); }
    }

    [Fact]
    public async Task Persisted_same_observation_Bollinger_centerline_qualifies_without_a_volatile_EMA_cache()
    {
        var clock = new ManualClock();
        var date = new DateOnly(2026, 11, 9);
        var contract = "TEST-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var market = Substitute.For<IMarketDataApi>();
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()).Returns(call =>
        { call[1] = new FuturesContractV3ReadModel { ContractId = contract }; return true; });
        var databases = Substitute.For<IDbContextFactory>();
        databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(series, date, TimeFrameType.FourHours, Arg.Any<CancellationToken>())
            .Returns(new FuturesBbSignalReadModel
            {
                Metadata = new() { ContractId = contract, SignalKey = new(series, MarketAnalyticsSignalKind.BollingerBand, TimeFrameType.FourHours, "test"),
                    MarketDataAsOfUtc = clock.GetUtcNow().AddMinutes(-1), IsValid = true },
                Ema20Center = 100, Width20Ratio = 1, Width20 = 2, Position20 = .5m, IsWarm = true
            });
        var reader = new IndicatorHealthReader(databases, market, new CmeFuturesMarketSessionCalendar(), clock,
            Substitute.For<IRegimeDiscoveryMarketSignalSnapshotProvider>());
        var rows = await reader.ReadAsync(date, default);
        Assert.True(Assert.Single(rows, row => row.TimeFrame == TimeFrameType.FourHours && row.Indicator == "Bollinger EMA20 centerline").Ready);
        Assert.DoesNotContain(rows, row => row.TimeFrame == TimeFrameType.Daily && row.Indicator == "TDI");
    }

    [Theory]
    [InlineData(TimeFrameType.FifteenSeconds)]
    [InlineData(TimeFrameType.OneMinute)]
    [InlineData(TimeFrameType.FiveMinutes)]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    [InlineData(TimeFrameType.FourHours)]
    public void Registry_requires_latest_completed_interval_not_a_new_four_hour_bar_every_five_minutes(TimeFrameType frame)
    {
        var at = new DateTimeOffset(2026, 11, 9, 11, 25, 0, TimeSpan.Zero);
        var expected = IndicatorHealthRegistry.MinimumSourceTime(frame, at, new CmeFuturesMarketSessionCalendar());
        Assert.Contains(frame, IndicatorHealthRegistry.IntradayFrames);
        Assert.True(IndicatorHealthRegistry.Evaluate("ES", frame, "ATR", true, true, expected.AddSeconds(1), expected).Ready);
        Assert.False(IndicatorHealthRegistry.Evaluate("ES", frame, "ATR", true, true, expected.AddSeconds(-1), expected).Ready);
        Assert.False(IndicatorHealthRegistry.Evaluate("ES", frame, "ATR", false, true, at, expected).Ready);
    }

    [Fact]
    public async Task Stalled_audit_raises_one_error_at_five_minutes_without_waiting_for_seed_or_feed()
    {
        var clock = new ManualClock();
        var reader = Substitute.For<IIndicatorHealthReader>();
        var pending = new TaskCompletionSource<IndicatorHealthEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.ReadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        var console = Substitute.For<IStatusConsoleEventProducer>();
        console.IsRunning.Returns(true);
        using var monitor = Create(reader, console, clock);
        await monitor.StartAsync(default);
        await Until(() => Task.FromResult(reader.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "ReadAsync")));
        clock.Advance(TimeSpan.FromMinutes(5).Subtract(TimeSpan.FromSeconds(1)));
        await Until(async () => (await Health(monitor)).Data["state"].ToString() == "Initializing");
        await console.DidNotReceive().PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>());
        clock.Advance(TimeSpan.FromSeconds(1));
        await Until(async () => (await Health(monitor)).Status == HealthStatus.Unhealthy);
        await Until(() => Task.FromResult(console.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "PublishConsoleAsync") == 1));
        clock.Advance(TimeSpan.FromMinutes(5));
        await Task.Delay(30);
        await console.Received(1).PublishConsoleAsync(Arg.Is<StatusConsoleLoggedEvent>(e => e.StatusConsoleLog.DataType == "IndicatorHealthFailure.v1"));
        await monitor.StopAsync(default);
        pending.TrySetCanceled();
    }

    [Fact]
    public async Task Warm_indicators_are_reaudited_after_five_minutes_and_regression_fails_without_new_grace_period()
    {
        var clock = new ManualClock();
        var reader = Substitute.For<IIndicatorHealthReader>();
        IndicatorHealthEntry[] rows = [new("ES", TimeFrameType.OneHour, "ATR14", true, true, clock.GetUtcNow(), null, "")];
        reader.ReadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => rows);
        var console = Substitute.For<IStatusConsoleEventProducer>();
        console.IsRunning.Returns(true);
        using var monitor = Create(reader, console, clock);
        await monitor.StartAsync(default);
        await Until(() => Task.FromResult(reader.ReceivedCalls().Any(c => c.GetMethodInfo().Name == "ReadAsync")));
        clock.Advance(TimeSpan.FromSeconds(5));
        await Until(async () => (await Health(monitor)).Status == HealthStatus.Healthy);
        rows = [rows[0] with { Valid = false, Reason = "Projection stale" }];
        clock.Advance(TimeSpan.FromMinutes(5));
        await Until(() => Task.FromResult(reader.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "ReadAsync") == 2));
        clock.Advance(TimeSpan.FromSeconds(5));
        await Until(async () => (await Health(monitor)).Status == HealthStatus.Unhealthy);
        await monitor.StopAsync(default);
    }

    [Fact]
    public void Restarted_unresolved_incident_keeps_its_original_deadline()
    {
        var started = DateTimeOffset.UtcNow;
        var incident = new IndicatorHealthMonitor.Incident(new(2026, 11, 9), started, Guid.NewGuid(), true, false);
        var encoded = System.Text.Json.JsonSerializer.Serialize(incident);
        var restored = System.Text.Json.JsonSerializer.Deserialize<IndicatorHealthMonitor.Incident>(encoded)!;
        Assert.False(IndicatorHealthMonitor.IsOverdue(restored, started.AddMinutes(5).AddTicks(-1)));
        Assert.True(IndicatorHealthMonitor.IsOverdue(restored, started.AddMinutes(5)));
        Assert.True(restored.Notified);
    }

    static IndicatorHealthMonitor Create(IIndicatorHealthReader reader, IStatusConsoleEventProducer console, ManualClock clock, string? root = null)
    {
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { ActiveValueDate = new(2026, 11, 9) });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Test");
        environment.ContentRootPath.Returns(root ?? Path.Combine(Path.GetTempPath(), "ifm-indicator-health-" + Guid.NewGuid().ToString("N")));
        return new(reader, sessions, clock, console, environment, NullLogger<IndicatorHealthMonitor>.Instance);
    }

    static Task<HealthCheckResult> Health(IndicatorHealthMonitor monitor) => monitor.CheckHealthAsync(new HealthCheckContext());

    static async Task Until(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition()) await Task.Delay(10, timeout.Token);
    }

    sealed class ManualClock : TimeProvider
    {
        DateTimeOffset now = new(2026, 11, 9, 11, 0, 0, TimeSpan.Zero);
        readonly List<ManualTimer> timers = [];
        public override DateTimeOffset GetUtcNow() => now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            timers.Add(timer);
            return timer;
        }
        public void Advance(TimeSpan elapsed)
        {
            now += elapsed;
            foreach (var timer in timers.ToArray()) timer.Tick();
        }
        sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            bool disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => !disposed;
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
            public void Tick() { if (!disposed) callback(state); }
        }
    }
}
