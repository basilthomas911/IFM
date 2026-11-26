using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class DailyFuturesExitReplayTests
{
    static FuturesPositionExitClockEvent DailyClock(int sequence)
    {
        var position = FuturesPositionDeliveryFixture.Input(sequence).PositionSnapshot with
        {
            RouteGeneration = 7,
            DailyFuturesEntry = new() { DcTriggerId = new("db83633c-5bcb-4335-81dd-dd1a67662409"), ContractId = "ESU6",
                ValueDate = new(2026, 8, 25), DcAtUtc = FuturesPositionDeliveryFixture.At, DcPrice = 5500, Threshold = 10 }
        };
        return DailyFuturesFlattenService.Create(position, new(2026, 8, 25, 20, 0, 0, DateTimeKind.Utc))!;
    }
    [Fact]
    public async Task Real_daily_flatten_clock_retry_preserves_the_same_exit_command()
    {
        await using var fixture = new FuturesPositionDeliveryFixture(false) { Failure = "exit-ambiguous" };
        fixture.Time.Now = new(2026, 8, 25, 20, 0, 0, TimeSpan.Zero); await fixture.Start();
        var source = DailyClock(1); Assert.False(await fixture.Deliver(source));
        var original = fixture.ExitAttempts.Single(); fixture.Time.Now += TimeSpan.FromMinutes(1); fixture.Failure = "none";
        Assert.True(await fixture.Deliver(source));
        Assert.Equal(MessagePackSerializer.Serialize(original), MessagePackSerializer.Serialize(fixture.ExitAttempts[1]));
    }
    [Fact]
    public async Task Later_daily_exit_observations_keep_the_original_authorizing_intent()
    {
        await using var fixture = new FuturesPositionDeliveryFixture(false); await fixture.Start();
        Assert.True(await fixture.Deliver(DailyClock(1))); Assert.True(await fixture.Deliver(DailyClock(2)));
        var first = Assert.Single(fixture.ExitAttempts);
        Assert.Single(fixture.Requests); // Later observations resume the captured close, not a new plan.
        var intent = fixture.Intents.Find(DailyClock(2).PositionSnapshot)!;
        Assert.Equal(MessagePackSerializer.Serialize(first), MessagePackSerializer.Serialize(intent.Command));
        var owner = new FuturesExitPositionWorkflowCommandState();
        Assert.True(first.Compute(owner, out var initial));
        owner.ReplayEvents(new IEvent[] { first.CreateExitPositionWorkflowStartedEvent(initial) });
        Assert.True(intent.Command.Compute(owner, out var duplicate)); Assert.True(duplicate.AlreadyStarted);
    }
}
