using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Services.MarketData;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class LivePipelineIntegrationTests
{
    static readonly DateOnly Date = new(2026, 9, 10);
    [Theory]
    [InlineData("Databento feed")]
    [InlineData("Native transport")]
    [InlineData("Native delivery")]
    [InlineData("Aggregation")]
    [InlineData("Price cache")]
    [InlineData("Price publication")]
    [InlineData("Tick storage")]
    public async Task Dataset_owned_upstream_stage_resets_only_after_five_continuous_minutes(string component)
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = component;
        await host.Monitor.CheckOnceAsync(default);
        var result = await host.Client.GetFromJsonAsync<LivePipelineHealthSnapshot>("api/market-data/live-health");
        Assert.Equal("Degraded", result!.Status);
        Assert.Contains(result.Checks, x => x.Component == component && x.Status == "Degraded");
        Assert.Equal(0, host.Probe.Resets);
        for (var minute = 1; minute < 5; minute++)
        {
            host.Time.Advance(TimeSpan.FromMinutes(1));
            await host.Monitor.CheckOnceAsync(default);
        }
        Assert.Equal(0, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        Assert.Empty(host.Probe.DownstreamRecoveries);
        Assert.False(result.AllowsNewDecisions);
    }

    [Theory]
    [InlineData("Bar timer", "2026-09-10")]
    [InlineData("Chart storage/query", "ES")]
    [InlineData("Analytics attachments", "RSI/FifteenSeconds")]
    [InlineData("Analytics processing", "MACD/OneMinute")]
    [InlineData("ITI", "ES")]
    [InlineData("Market Outlook publication", "ES")]
    public async Task Downstream_stage_gets_targeted_recovery_after_one_minute_and_full_reset_after_five(
        string component, string scope)
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = component;
        host.Probe.FailureScope = scope;
        await host.Monitor.CheckOnceAsync(default);
        Assert.Empty(host.Probe.DownstreamRecoveries);
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Contains(host.Probe.DownstreamRecoveries, check => check.Component == component && check.Scope == scope);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        Assert.Single(host.Probe.DownstreamRecoveries,
            check => check.Component == component && check.Scope == scope);
    }

    [Fact]
    public async Task Downstream_recovery_failure_is_retained_in_component_health_details()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Bar timer";
        host.Probe.FailureScope = Date.ToString("yyyy-MM-dd");
        host.Probe.RecoveryFailure = new InvalidOperationException("Injected chart restart rejection");
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.Monitor.CheckOnceAsync(default);
        var check = Assert.Single(host.Monitor.Current.Checks, item => item.Component == "Bar timer");
        Assert.Equal(1, check.RecoveryAttempts);
        Assert.Contains("Failed: Injected chart restart rejection", check.RecoveryState);
        Assert.Equal(0, host.Probe.Resets);
    }

    [Theory]
    [InlineData("Messaging transport")]
    [InlineData("Actor routing")]
    [InlineData("UI delivery")]
    public async Task Required_infrastructure_failures_hard_reset_but_optional_ui_does_not(string component)
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = component;
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(6));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(component == "UI delivery" ? 0 : 1, host.Probe.Resets);
        Assert.Empty(host.Probe.DownstreamRecoveries);
    }

    [Fact]
    public async Task Downstream_recovery_runs_during_the_hard_reset_confirmation_window()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Native delivery";
        host.Probe.AdditionalFailure = ("Bar timer", Date.ToString("yyyy-MM-dd"));
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Contains(host.Probe.DownstreamRecoveries, check => check.Component == "Bar timer");
        host.Probe.Failure = null;
        await host.Monitor.CheckOnceAsync(default);
        Assert.Contains(host.Probe.DownstreamRecoveries, check => check.Component == "Bar timer");
        Assert.Equal(0, host.Probe.Resets);
    }

    [Fact]
    public async Task Healthy_observation_clears_window_and_each_reset_starts_a_new_window()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Native delivery";
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(4)); await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(0, host.Probe.Resets);
        host.Probe.Failure = null;
        await host.Monitor.CheckOnceAsync(default);
        host.Probe.Failure = "Native delivery";
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(5)); await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromMinutes(4)); await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromMinutes(1)); await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        Assert.Contains(host.Monitor.Current.Checks, check => check.RecoveryState.StartsWith("HardResetRecoveryFailed", StringComparison.Ordinal));
        Assert.Equal("Degraded", host.Monitor.Current.Status);
        host.Probe.Failure = null;
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal("Healthy", host.Monitor.Current.Status);
        host.Time.Advance(TimeSpan.FromSeconds(91));
        Assert.Equal("Unknown", host.Monitor.Current.Status);
        Assert.False(host.Monitor.Current.AllowsNewDecisions);
    }

    [Fact]
    public async Task Each_upstream_component_has_its_own_five_minute_window()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Native delivery";
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(4));
        await host.Monitor.CheckOnceAsync(default);
        host.Probe.Failure = "Aggregation";
        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(0, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
    }

    [Fact]
    public async Task Hard_reset_failure_is_retained_on_the_failed_component()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Aggregation";
        host.Probe.ResetFailure = new IOException("Injected hard reset failure");
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);
        var check = Assert.Single(host.Monitor.Current.Checks, item => item.Component == "Aggregation");
        Assert.Equal(1, check.RecoveryAttempts);
        Assert.Contains("HardResetFailed: Injected hard reset failure", check.RecoveryState);
        Assert.Equal("Degraded", host.Monitor.Current.Status);
    }

    [Fact]
    public async Task Configured_startup_boundary_forces_one_hard_reset_at_exactly_five_minutes()
    {
        await using var host = await Harness.StartAsync(new LivePipelineMonitorOptions
        {
            HardResetDelay = TimeSpan.FromMinutes(5),
            RecoveryObservationWindow = TimeSpan.FromMinutes(5),
            ForceOneHardResetAfterStartup = true
        });
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(0, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);
    }
    [Fact]
    public async Task Failure_observed_after_forced_reset_uses_the_same_recovery_window_without_a_second_reset()
    {
        await using var host = await Harness.StartAsync(new LivePipelineMonitorOptions
        {
            HardResetDelay = TimeSpan.FromMinutes(5),
            RecoveryObservationWindow = TimeSpan.FromMinutes(5),
            ForceOneHardResetAfterStartup = true
        });
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Resets);

        host.Probe.Failure = "Aggregation";
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await host.Monitor.CheckOnceAsync(default);

        Assert.Equal(1, host.Probe.Resets);
        Assert.Contains(host.Monitor.Current.Checks,
            check => check.Component == "Aggregation"
                && check.RecoveryState.StartsWith("HardResetRecoveryFailed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Planned_closure_never_resets_datasets()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Session";
        host.Probe.Active = false;
        await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromHours(1));
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(0, host.Probe.Resets);
    }

    [Fact]
    public async Task Real_ui_http_client_reports_receipt_and_rendering_independently()
    {
        await using var host = await Harness.StartAsync();
        var now = host.Time.GetUtcNow().UtcDateTime;
        using var service = new MarketDataOperationsHealthQueryService(host.Client,
            new Uri(host.Client.BaseAddress!, "api/market-data/operations-health"));
        var id = Guid.NewGuid();
        await service.ReportAndCheckAsync(new(id, Date, [new("ES", now, null), new("VX", now, now)]));
        var checks = host.Evidence.CheckUi(Date, new Dictionary<string, DateTime> { ["ES"] = now, ["VX"] = now });
        Assert.Contains(checks, x => x.Component == "UI delivery" && x.Scope.EndsWith("ES") && x.Status == "Healthy");
        Assert.Contains(checks, x => x.Component == "UI rendering" && x.Scope.EndsWith("ES") && x.Status == "Degraded");
        await service.ReportAndCheckAsync(new(id, Date, [new("ES", now, now), new("VX", now, now)]));
        Assert.All(host.Evidence.CheckUi(Date, new Dictionary<string, DateTime> { ["ES"] = now }), x => Assert.Equal("Healthy", x.Status));
        host.Time.Advance(TimeSpan.FromMinutes(3));
        Assert.Contains(host.Evidence.CheckUi(Date, new Dictionary<string, DateTime> { ["ES"] = now }), x => x.Status == "Unknown");
    }

    [Fact]
    public async Task Invalid_or_future_ui_evidence_is_rejected()
    {
        await using var host = await Harness.StartAsync();
        var now = host.Time.GetUtcNow().UtcDateTime;
        using var response = await host.Client.PostAsJsonAsync("api/market-data/live-health/ui",
            new LiveUiHealthReport(Guid.NewGuid(), Date, [new("ES", now.AddHours(1), now.AddHours(1))]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Connected_ui_must_match_current_value_date_and_contract()
    {
        var time = new ManualTime();
        var evidence = new LivePipelineEvidence(time);
        var now = time.GetUtcNow().UtcDateTime;
        var id = Guid.NewGuid();
        var latest = new Dictionary<string, DateTime> { ["ES"] = now };
        var contracts = new Dictionary<string, string> { ["ES"] = "ES-current" };
        Assert.True(evidence.ReportUi(new(id, Date.AddDays(-1), [new("ES", now, now, "ES-current")])));
        Assert.Contains(evidence.CheckUi(Date, latest, contracts), x => x.Component == "UI value date" && x.Required && x.Status == "Degraded");
        Assert.True(evidence.ReportUi(new(id, Date, [new("ES", now, now, "ES-old")])));
        Assert.Contains(evidence.CheckUi(Date, latest, contracts), x => x.Component == "UI contract" && x.Status == "Degraded");
        Assert.True(evidence.ReportUi(new(id, Date, [new("ES", now, now, "ES-current")])));
        Assert.All(evidence.CheckUi(Date, latest, contracts), x => Assert.Equal("Healthy", x.Status));
    }

    [Fact]
    public async Task Real_bar_timer_is_idempotent_and_stopped_timer_is_not_healthy()
    {
        var timer = new FuturesBarDataTimer(TimeSpan.FromMilliseconds(10));
        var id = new FuturesBarDataStreamingId(Date);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        Assert.False(timer.IsRunning(id));
        Assert.True(timer.Start(id, () => { Interlocked.Increment(ref calls); completed.TrySetResult(); return ValueTask.CompletedTask; }));
        Assert.False(timer.Start(id, () => throw new Exception("Duplicate timer")));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(timer.IsRunning(id));
        await timer.StopAllAsync();
        Assert.False(timer.IsRunning(id));
        var stoppedCount = calls;
        await Task.Delay(40);
        Assert.Equal(stoppedCount, calls);
    }

    [Fact]
    public void No_clients_and_no_evidence_are_unknown_not_healthy()
    {
        var evidence = new LivePipelineEvidence(new ManualTime());
        Assert.Null(evidence.Get("ITI", "ES"));
        Assert.Contains(evidence.CheckUi(Date, new Dictionary<string, DateTime>()), x => x.Status == "Unknown");
    }

    [Fact]
    public async Task Hosted_monitor_checks_every_minute_and_does_not_overlap()
    {
        var time = new ManualTime(); var probe = new Probe(time);
        using var monitor = new LivePipelineMonitor(probe, time, NullLogger<LivePipelineMonitor>.Instance);
        await monitor.StartAsync(default);
        await Until(() => probe.Checks == 1 && time.TimerCount > 0);
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(1, probe.Checks);
        time.Advance(TimeSpan.FromSeconds(1));
        await Until(() => probe.Checks == 2);
        probe.CheckGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = monitor.CheckOnceAsync(default);
        await Until(() => probe.Checks == 3);
        await monitor.CheckOnceAsync(default);
        Assert.Equal(3, probe.Checks);
        probe.CheckGate.SetResult(); await pending;
        await monitor.StopAsync(default);
    }

    [Fact]
    public async Task Faulted_bar_timer_can_be_rearmed_without_duplicate_callbacks()
    {
        var timer = new FuturesBarDataTimer(TimeSpan.FromMilliseconds(10));
        var id = new FuturesBarDataStreamingId(Date);
        timer.Start(id, () => throw new IOException("Injected callback failure"));
        await Until(() => !timer.IsRunning(id));
        var progressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(timer.Start(id, () => { progressed.TrySetResult(); return ValueTask.CompletedTask; }));
        await progressed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await timer.StopAllAsync();
    }

    static async Task Until(Func<bool> predicate)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!predicate()) await Task.Delay(5, deadline.Token);
    }

    public sealed class ManualTime : TimeProvider
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        readonly List<TestTimer> timers = [];
        public int TimerCount { get { lock (timers) return timers.Count; } }
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration)
        {
            now += duration;
            TestTimer[] snapshot; lock (timers) snapshot = timers.ToArray();
            foreach (var timer in snapshot) timer.Fire(now);
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new TestTimer(this, callback, state); timer.Change(dueTime, period);
            lock (timers) timers.Add(timer);
            return timer;
        }
        sealed class TestTimer(ManualTime time, TimerCallback callback, object? state) : ITimer
        {
            DateTimeOffset next;
            TimeSpan period;
            bool disposed;
            public bool Change(TimeSpan due, TimeSpan interval)
            { next = due == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : time.now + due; period = interval; return !disposed; }
            public void Fire(DateTimeOffset now)
            {
                if (disposed || now < next) return;
                next = period > TimeSpan.Zero ? now + period : DateTimeOffset.MaxValue;
                callback(state);
            }
            public void Dispose() => disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
    sealed class Probe(ManualTime time) : ILivePipelineProbe
    {
        public string? Failure;
        public string FailureScope = "ES";
        public (string Component, string Scope)? AdditionalFailure;
        public bool Active = true;
        public int Resets;
        public int Checks;
        public List<LivePipelineCheck> DownstreamRecoveries { get; } = [];
        public Exception? RecoveryFailure;
        public Exception? ResetFailure;
        public TaskCompletionSource? CheckGate;
        public async Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Checks);
            if (CheckGate is { } pending) await pending.Task.WaitAsync(token);
            var now = time.GetUtcNow().UtcDateTime;
            var upstream = new[]
            {
                ("Databento feed", "datasets"), ("Native transport", "GLBX.MDP3"),
                ("Native delivery", "GLBX.MDP3"), ("Aggregation", "GLBX.MDP3"),
                ("Price cache", "ES"), ("Price publication", "ES"), ("Tick storage", "ES")
            };
            var checks = upstream.Select(item => new LivePipelineCheck(
                item.Item1, item.Item2,
                Failure == item.Item1 ? "Degraded" : "Healthy",
                "Injected stage observation", now)).ToList();
            if (Failure is not null && upstream.All(item => item.Item1 != Failure))
                checks.Add(new(Failure, FailureScope, "Degraded", "Injected stage observation", now));
            if (AdditionalFailure is { } additional)
                checks.Add(new(additional.Component, additional.Scope, "Degraded", "Injected additional stage observation", now));
            return new LivePipelineHealthSnapshot(
                now, Active ? Date : null,
                Failure is null && AdditionalFailure is null ? "Healthy" : "Degraded", checks);
        }
        public Task HardResetAsync(LivePipelineHealthSnapshot unhealthySnapshot, CancellationToken token)
        {
            Resets++;
            return ResetFailure is null ? Task.CompletedTask : Task.FromException(ResetFailure);
        }
        public Task RecoverDownstreamAsync(LivePipelineCheck unhealthyCheck, DateOnly valueDate, CancellationToken token)
        {
            DownstreamRecoveries.Add(unhealthyCheck);
            return RecoveryFailure is null ? Task.CompletedTask : Task.FromException(RecoveryFailure);
        }
    }
    sealed class Harness(WebApplication app, HttpClient client, ManualTime time, Probe probe,
        LivePipelineMonitor monitor, LivePipelineEvidence evidence) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public ManualTime Time => time;
        public Probe Probe => probe;
        public LivePipelineMonitor Monitor => monitor;
        public LivePipelineEvidence Evidence => evidence;
        public static async Task<Harness> StartAsync(LivePipelineMonitorOptions? options = null)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var time = new ManualTime(); var probe = new Probe(time);
            var evidence = new LivePipelineEvidence(time);
            var monitor = new LivePipelineMonitor(probe, time, NullLogger<LivePipelineMonitor>.Instance, configuredOptions: options);
            builder.Services.AddSingleton(evidence); builder.Services.AddSingleton(monitor);
            var app = builder.Build(); app.MapLivePipelineHealth(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, time, probe, monitor, evidence);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
