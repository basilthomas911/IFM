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
    [InlineData("Messaging transport")]
    [InlineData("Price cache")]
    [InlineData("Price publication")]
    [InlineData("Tick storage")]
    [InlineData("Bar timer")]
    [InlineData("Chart storage/query")]
    [InlineData("ITI")]
    [InlineData("Analytics attachments")]
    [InlineData("UI delivery")]
    [InlineData("UI rendering")]
    public async Task Failed_stage_reaches_http_and_recovery_does_not_claim_success(string component)
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = component;
        await host.Monitor.CheckOnceAsync(default);
        var result = await host.Client.GetFromJsonAsync<LivePipelineHealthSnapshot>("api/market-data/live-health");
        Assert.Equal("Degraded", result!.Status);
        Assert.Contains(result.Checks, x => x.Component == component && x.Status == "Degraded");
        Assert.Equal(1, host.Probe.Recoveries);
        Assert.False(result.AllowsNewDecisions && !component.StartsWith("UI"));
    }

    [Fact]
    public async Task Recovery_is_bounded_backed_off_and_verified_on_later_observation()
    {
        await using var host = await Harness.StartAsync();
        host.Probe.Failure = "Bar timer";
        await host.Monitor.CheckOnceAsync(default);
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(1, host.Probe.Recoveries);
        host.Time.Advance(TimeSpan.FromMinutes(1)); await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(2)); await host.Monitor.CheckOnceAsync(default);
        host.Time.Advance(TimeSpan.FromMinutes(8)); await host.Monitor.CheckOnceAsync(default);
        Assert.Equal(3, host.Probe.Recoveries);
        Assert.Equal("Degraded", host.Monitor.Current.Status);
        host.Probe.Failure = null;
        await host.Monitor.CheckOnceAsync(default);
        Assert.Equal("Healthy", host.Monitor.Current.Status);
        host.Time.Advance(TimeSpan.FromSeconds(91));
        Assert.Equal("Unknown", host.Monitor.Current.Status);
        Assert.False(host.Monitor.Current.AllowsNewDecisions);
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

    sealed class ManualTime : TimeProvider
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
        public bool CanRecover(LivePipelineCheck failure) => true;
        public string? Failure;
        public int Recoveries;
        public int Checks;
        public TaskCompletionSource? CheckGate;
        public async Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token)
        {
            Interlocked.Increment(ref Checks);
            if (CheckGate is { } pending) await pending.Task.WaitAsync(token);
            return new LivePipelineHealthSnapshot(
            time.GetUtcNow().UtcDateTime, Date, Failure is null ? "Healthy" : "Degraded",
            [new(Failure ?? "Feed", "ES", Failure is null ? "Healthy" : "Degraded", "Injected stage observation", time.GetUtcNow().UtcDateTime)]);
        }
        public Task RecoverAsync(LivePipelineCheck failure, CancellationToken token) { Recoveries++; return Task.CompletedTask; }
    }
    sealed class Harness(WebApplication app, HttpClient client, ManualTime time, Probe probe,
        LivePipelineMonitor monitor, LivePipelineEvidence evidence) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public ManualTime Time => time;
        public Probe Probe => probe;
        public LivePipelineMonitor Monitor => monitor;
        public LivePipelineEvidence Evidence => evidence;
        public static async Task<Harness> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var time = new ManualTime(); var probe = new Probe(time);
            var evidence = new LivePipelineEvidence(time);
            var monitor = new LivePipelineMonitor(probe, time, NullLogger<LivePipelineMonitor>.Instance);
            builder.Services.AddSingleton(evidence); builder.Services.AddSingleton(monitor);
            var app = builder.Build(); app.MapLivePipelineHealth(); await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, time, probe, monitor, evidence);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    }
}
