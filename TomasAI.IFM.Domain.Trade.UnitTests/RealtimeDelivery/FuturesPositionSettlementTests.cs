using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class FuturesPositionSettlementTests
{
    public static IEnumerable<object[]> Cases => new[] { "Position", "Clock" }.SelectMany(kind => new[] { false, true }.SelectMany(metrics =>
        new[] { "throw", "terminal", "lost-reply", "exit-send", "exit-ambiguous" }.Select(failure => new object[] { kind, metrics, failure })));
    [Theory, MemberData(nameof(Cases))]
    public async Task Required_failures_fail_delivery_and_retry_original_commands(string kind, bool metrics, string failure)
    {
        await using var fixture = new FuturesPositionDeliveryFixture(metrics) { Failure = failure }; await fixture.Start();
        var source = FuturesPositionDeliveryFixture.Input(kind, 1); Assert.False(await fixture.Deliver(source));
        var original = fixture.Requests.Single(); fixture.Failure = "none";
        Assert.True(await fixture.Deliver(source)); Assert.True(await fixture.Deliver(FuturesPositionDeliveryFixture.Input(kind, 2)));
        // A frozen exit output bypasses a second Function call entirely; the original request remains durable.
        var stored=await fixture.PositionIntents.FindAsync(original.CommandId,PositionRealtimeIntents.Hash(source.State));
        var replay=MessagePackSerializer.Deserialize<UpdateFuturesTradePlanCommand>(stored!.Request);
        Assert.Equal(original.CommandId,replay.CommandId);Assert.Equal(original.Fingerprint(),replay.Fingerprint());
        Assert.Equal(original.RequestedAtUtc,replay.RequestedAtUtc);
        Assert.Equal(2, fixture.Receipts.Count);
        Assert.Equal(new long[] { 1, 2 }, fixture.AcceptedExits.Select(command => command.ExitPlan.Plan.PlanRevision).Distinct());
        if (fixture.ExitAttempts.Count > 2)
            Assert.Equal(MessagePackSerializer.Serialize(fixture.ExitAttempts[0]), MessagePackSerializer.Serialize(fixture.ExitAttempts[1]));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Plan_without_exit_requires_no_exit_handoff(bool metrics)
    {
        await using var fixture = new FuturesPositionDeliveryFixture(metrics) { RequiresExit = false }; await fixture.Start();
        Assert.True(await fixture.Deliver(FuturesPositionDeliveryFixture.Input(1)));
        Assert.Single(fixture.Receipts); Assert.Empty(fixture.ExitAttempts);
    }
    [Theory]
    [InlineData("Position", false)] [InlineData("Position", true)]
    [InlineData("Clock", false)] [InlineData("Clock", true)]
    public async Task Pending_required_plan_does_not_acknowledge_early(string kind, bool metrics)
    {
        await using var fixture = new FuturesPositionDeliveryFixture(metrics) { Pause = new(TaskCreationOptions.RunContinuationsAsynchronously) }; await fixture.Start();
        var work = fixture.Deliver(FuturesPositionDeliveryFixture.Input(kind, 1));
        try { await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(work.IsCompleted); Assert.Empty(fixture.ExitAttempts); }
        finally { fixture.Pause.TrySetResult(); }
        Assert.True(await work);
    }
    [Fact]
    public async Task Later_retry_time_keeps_fingerprint_and_restored_function_receipt_with_unchanged_parameters()
    {
        await using var fixture = new FuturesPositionDeliveryFixture(false) { Failure = "lost-reply" }; await fixture.Start();
        var source = FuturesPositionDeliveryFixture.Input(1); Assert.False(await fixture.Deliver(source));
        var original = fixture.Requests.Single(); fixture.Time.Now += TimeSpan.FromDays(1); fixture.Failure = "none";
        Assert.True(await fixture.Deliver(source)); var replay = fixture.Requests[1];
        Assert.Equal(original.RequestedAtUtc, replay.RequestedAtUtc); Assert.Equal(original.Fingerprint(), replay.Fingerprint());
        var state = new FuturesTradePlanFunctionState(); state.ReplayEvents(new IEvent[] { fixture.Receipts[original.CommandId] });
        state.Prepare(replay); Assert.True(state.IsCompleted); Assert.True(state.Matches(replay));
        Assert.Equal(original.RequestedAtUtc, fixture.AcceptedExits.Single().ExitPlan.Plan.CalculatedAtUtc);
    }
}
