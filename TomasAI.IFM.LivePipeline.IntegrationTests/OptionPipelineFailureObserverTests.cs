using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionPipelineFailureObserverTests
{
    static readonly DateOnly Date = new(2026, 10, 22);
    internal static ActorMailboxMetricsSnapshot Mailbox() => new(
        new(ActorType.Realtime, "IronCondorTradePositionRealtime", "101.701.1901.1501.position"),
        2048, 2048, 2048, 100, 5, 4, 0, 0, 0, 50, false,
        ActorMailboxLifecycleState.Quarantined, 1, true, "StrategyPositionChanged",
        null, null, null, null, "", "", 1, LastRejectionReason: "Stopping");

    [Fact]
    public void Failure_has_one_two_minute_deadline_and_retries_until_acknowledged()
    {
        var clock = new Clock(); var observer = new OptionPipelineFailureObserver(clock); var mailbox = Mailbox();
        Assert.Empty(observer.Observe(Date, [mailbox]));
        clock.Advance(119); Assert.Empty(observer.Observe(Date, [mailbox with { Generation = 2 }]));
        clock.Advance(1); var first = Assert.Single(observer.Observe(Date, [mailbox]));
        Assert.Contains("120 seconds", first.ErrorMessage);
        Assert.Contains("automatic API restart is disabled", first.ErrorMessage);
        Assert.Equal("Options", first.Role);
        Assert.Equal(first.CommandId, Assert.Single(observer.Observe(Date, [mailbox])).CommandId);
        observer.MarkPublished(first.CommandId); Assert.Empty(observer.Observe(Date, [mailbox]));
        clock.Advance(10); Assert.Equal(first.CommandId, Assert.Single(observer.Observe(Date, [mailbox])).CommandId);
    }

    [Fact]
    public void Healthy_recovery_and_market_close_clear_the_episode()
    {
        var clock = new Clock(); var observer = new OptionPipelineFailureObserver(clock); var mailbox = Mailbox();
        observer.Observe(Date, [mailbox]); clock.Advance(119);
        var healthy = mailbox with { IsAdmissionOpen = true, LifecycleState = ActorMailboxLifecycleState.Running, QueueDepth = 0, IsProcessing = false };
        Assert.Empty(observer.Observe(Date, [healthy])); clock.Advance(2);
        Assert.Empty(observer.Observe(Date, [mailbox])); clock.Advance(120);
        var first = Assert.Single(observer.Observe(Date, [mailbox]));
        Assert.Empty(observer.Observe(null, [mailbox])); clock.Advance(120);
        Assert.Empty(observer.Observe(Date.AddDays(1), [mailbox])); clock.Advance(120);
        Assert.NotEqual(first.CommandId, Assert.Single(observer.Observe(Date.AddDays(1), [mailbox])).CommandId);
    }

    [Fact]
    public void Futures_and_idle_mailboxes_do_not_trigger_but_saturation_and_hung_handlers_do()
    {
        var clock = new Clock(); var observer = new OptionPipelineFailureObserver(clock);
        var futures = Mailbox() with { ThreadId = new(ActorType.Realtime, "FuturesItiSignalRealtime", "ES") };
        observer.Observe(Date, [futures]); clock.Advance(120); Assert.Empty(observer.Observe(Date, [futures]));
        var full = Mailbox() with { IsAdmissionOpen = true, LifecycleState = ActorMailboxLifecycleState.Running, IsProcessing = false };
        Assert.Empty(observer.Observe(Date, [full])); clock.Advance(120);
        Assert.Contains("saturated", Assert.Single(observer.Observe(Date, [full])).ErrorMessage);
        var hung = full with { QueueDepth = 0, IsProcessing = true, LastStartedUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-2) };
        Assert.Contains("handler", Assert.Single(observer.Observe(Date, [hung])).ErrorMessage);
    }

    internal sealed class Clock : TimeProvider
    {
        long elapsed;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => elapsed * 1000;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero).AddSeconds(elapsed);
        public void Advance(int seconds) => elapsed += seconds;
    }
}
