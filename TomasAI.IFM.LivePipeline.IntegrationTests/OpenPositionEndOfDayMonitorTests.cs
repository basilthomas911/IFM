using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.Recovery;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OpenPositionEndOfDayMonitorTests
{
    static readonly DateOnly Date = new(2026, 11, 17);
    static readonly DateTimeOffset Now = new(2026, 11, 17, 23, 5, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recovery_worker_storage_failure_blocks_readiness_even_when_no_new_seals_are_needed(bool storageFails)
    {
        var jobs=Substitute.For<IPositionEndOfDayRecoveryStore>();
        jobs.ClaimAsync(Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>())
            .Returns(_=>storageFails?Task.FromException<PositionEndOfDayJob?>(new InvalidOperationException("Injected unavailable recovery schema")):Task.FromResult<PositionEndOfDayJob?>(null));
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);
        var recovery=new PositionEndOfDayRecovery(jobs,Substitute.For<IOpenPositionEndOfDayStore>(),Substitute.For<IPositionClosingQuoteSource>(),Substitute.For<IStrategyPositionCommandApi>(),TimeProvider.System,new());
        using var worker=new PositionEndOfDayRecoveryWorker(recovery,supervisor,NullLogger<PositionEndOfDayRecoveryWorker>.Instance,TimeProvider.System);
        await worker.StartAsync(default);
        var expected=storageFails?HealthStatus.Unhealthy:HealthStatus.Healthy;
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        HealthCheckResult health;
        do {
            health=await worker.CheckHealthAsync(new());
            if(health.Status!=expected) await Task.Delay(10,deadline.Token);
        } while(health.Status!=expected);
        Assert.Equal(expected,health.Status);
        await worker.StopAsync(default);
    }

    [Fact]
    public async Task Older_unfinished_quote_recovery_keeps_readiness_blocked_after_the_target_date_advances()
    {
        var jobs=Substitute.For<IPositionEndOfDayRecoveryStore>();
        jobs.CountOutstandingAsync(Arg.Any<DateTimeOffset>(),Arg.Any<CancellationToken>()).Returns(1);
        jobs.ClaimAsync(Arg.Any<DateTimeOffset>(),Arg.Any<TimeSpan>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<PositionEndOfDayJob?>(null));
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);
        var recovery=new PositionEndOfDayRecovery(jobs,Substitute.For<IOpenPositionEndOfDayStore>(),Substitute.For<IPositionClosingQuoteSource>(),Substitute.For<IStrategyPositionCommandApi>(),TimeProvider.System,new());
        using var worker=new PositionEndOfDayRecoveryWorker(recovery,supervisor,NullLogger<PositionEndOfDayRecoveryWorker>.Instance,TimeProvider.System);
        await worker.StartAsync(default);
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        HealthCheckResult health;
        do { health=await worker.CheckHealthAsync(new());if(health.Status!=HealthStatus.Unhealthy)await Task.Delay(10,deadline.Token); } while(health.Status!=HealthStatus.Unhealthy);
        Assert.Contains("unfinished",health.Description);await worker.StopAsync(default);
    }

    [Fact]
    public async Task Hosted_monitor_wakes_at_new_session_close_and_a_fresh_monitor_audits_again()
    {
        var clock = new MutableClock(DateTimeOffset.Parse("2026-11-18T21:59:59Z"));
        var initial = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var storage = Substitute.For<IOpenPositionEndOfDayStore>();
        storage.ReadOpenAsync(null, 64, Arg.Any<CancellationToken>()).Returns(_ => {
            var count = Interlocked.Increment(ref calls);
            if (count == 1) initial.TrySetResult();
            else if (count == 2) closed.TrySetResult();
            else startup.TrySetResult();
            return Task.FromResult(Array.Empty<StrategyPositionSnapshot>());
        });
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true);
        var sweep = new OpenPositionEndOfDaySweep(storage, Substitute.For<IStrategyPositionCommandApi>(), new CmeFuturesMarketSessionCalendar(), clock, new());
        OpenPositionEndOfDayMonitor Create() => new(sweep, supervisor, clock, NullLogger<OpenPositionEndOfDayMonitor>.Instance);
        using (var monitor = Create())
        {
            await monitor.StartAsync(default);
            await initial.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Now = DateTimeOffset.Parse("2026-11-18T22:00:00Z");
            // The session date change bypasses the usual 30-second audit interval.
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await monitor.StopAsync(default);
            Assert.Equal(HealthStatus.Healthy, (await monitor.CheckHealthAsync(new())).Status);
        }
        using (var restarted = Create())
        {
            await restarted.StartAsync(default);
            await startup.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await restarted.StopAsync(default);
            Assert.Equal(HealthStatus.Healthy, (await restarted.CheckHealthAsync(new())).Status);
        }
        Assert.Equal(3, calls);
    }

    sealed class MutableClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData("MissingSeal")]
    [InlineData("StaleCloseObservation")]
    [InlineData("AuditFailed")]
    public void Missing_or_stale_position_evidence_fails_readiness(string code)
    {
        var audit = new OpenPositionEndOfDayAudit(Date, Now, 1, 0, 0, 0, 1,
            [new("101.703.4201.4601.position", Date, code, "Unqualified EOD evidence")]);
        var health = OpenPositionEndOfDayMonitor.Evaluate(audit, "", Date, Now);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Contains(code, health.Description);
        Assert.Contains("4601", health.Description);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("expired")]
    [InlineData("rollover")]
    [InlineData("error")]
    public void Old_or_failed_audits_cannot_leave_a_healthy_readiness_snapshot(string state)
    {
        var audit = state == "none" ? null : new OpenPositionEndOfDayAudit(Date,
            state == "expired" ? Now.AddMinutes(-3) : Now, 1, 0, 0, 0, 0, []);
        var health = OpenPositionEndOfDayMonitor.Evaluate(audit, state == "error" ? "Storage unavailable" : "",
            state == "rollover" ? Date.AddDays(1) : Date, Now);
        Assert.NotEqual(HealthStatus.Healthy, health.Status);
    }

    [Fact]
    public void Fresh_completed_sweep_with_no_failures_is_ready()
    {
        var audit = new OpenPositionEndOfDayAudit(Date, Now, 4, 3, 3, 1, 0, []);
        Assert.Equal(HealthStatus.Healthy, OpenPositionEndOfDayMonitor.Evaluate(audit, "", Date, Now).Status);
    }
}
