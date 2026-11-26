using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class IronCondorPositionSettlementTests
{
    public static IEnumerable<object[]> Cases => new[] { "Position", "Monitoring" }.SelectMany(kind => new[] { false, true }.SelectMany(metrics => new[] { "throw", "terminal", "lost-reply", "publication" }.Select(failure => new object[] { kind, metrics, failure })));
    [Theory, MemberData(nameof(Cases))]
    public async Task Required_failure_retries_with_the_same_commands_and_no_duplicate_plan_receipts(string kind, bool metrics, string failure)
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(metrics) { Failure = failure }; await fixture.Start();
        var source = fixture.Input(kind, 1);
        Assert.False(await fixture.Deliver(source)); Assert.Empty(fixture.PublishedPlans);
        var first = fixture.PlanRequests.Single(); fixture.Failure = "none";
        Assert.True(await fixture.Deliver(source)); Assert.True(await fixture.Deliver(fixture.Input(kind, 2)));
        Assert.Equal(new long[] { 1, 2 }, fixture.PublishedPlans); Assert.Equal(2, fixture.Receipts.Count);
        Assert.Equal(first.CommandId, fixture.PlanRequests[1].CommandId); Assert.Equal(first.Fingerprint(), fixture.PlanRequests[1].Fingerprint());
        Assert.Equal(2, fixture.ForwardRequests.Count); // retry reuses the frozen plan; successor gets its own forward request.
    }
    [Theory]
    [InlineData("Position")] [InlineData("Monitoring")]
    public async Task Pending_function_does_not_settle_early(string kind)
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { Pause = new(TaskCreationOptions.RunContinuationsAsynchronously) }; await fixture.Start();
        var work = fixture.Deliver(fixture.Input(kind, 1)); await fixture.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(work.IsCompleted); Assert.Empty(fixture.PublishedPlans); fixture.Pause.TrySetResult(); Assert.True(await work);
    }
    [Theory]
    [InlineData("superseded")] [InlineData("closing")] [InlineData("retired")]
    public async Task Intentional_obsolete_or_closing_skips_remain_successful(string skip)
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { Failure = skip == "superseded" ? skip : "none", Closing = skip == "closing", Current = skip != "retired" }; await fixture.Start();
        Assert.True(await fixture.Deliver(fixture.Input("Monitoring", 1))); Assert.Empty(fixture.Receipts);
    }
    [Fact]
    public async Task Position_redelivery_preserves_original_forward_request_despite_time_drift()
    {
        // The real restored Function state must accept the durably captured original request.
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { Failure = "lost-reply" }; await fixture.Start();
        var source = fixture.Input("Position", 1); Assert.False(await fixture.Deliver(source));
        var original = fixture.ForwardRequests.Single(); fixture.Time.Now += TimeSpan.FromSeconds(1);
        fixture.Failure = "none"; Assert.True(await fixture.Deliver(source));
        var receipt = fixture.Requests.Find(source.Id, IronCondorPositionChanged.SourceFingerprint((IronCondorPositionChangedEvent)source, new(2026,8,25)))!;
        var replay = receipt.Forward; Assert.Single(fixture.ForwardRequests);
        Assert.Equal(original.CommandId, replay.CommandId); Assert.Equal(original.Fingerprint(), replay.Fingerprint());
        var state = new IronCondorForwardPriceFunctionState();
        state.ReplayEvents(new IEvent[] { new IronCondorForwardPriceCalculatedEvent { Id = original.CommandId, CommandId = original.CommandId, EntityId = original.EntityId, RequestFingerprint = original.Fingerprint() } });
        state.Prepare(replay); Assert.True(state.IsCompleted); Assert.True(state.Matches(replay));
    }
    [Fact]
    public async Task Changed_market_inputs_and_parameters_cannot_change_committed_plan_request_on_retry()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { Failure = "lost-reply" }; await fixture.Start();
        var source = fixture.Input("Position", 1); Assert.False(await fixture.Deliver(source)); var original = fixture.PlanRequests.Single();
        fixture.Inputs = fixture.Inputs with { PutForwardPrice = 99 }; fixture.Parameters = new() { Version = 2, MaximumLoss = 2000 }; fixture.Failure = "none"; Assert.True(await fixture.Deliver(source)); var replay = fixture.PlanRequests[1];
        Assert.Equal(original.CommandId, replay.CommandId); Assert.Equal(original.Fingerprint(), replay.Fingerprint());
        var state = new IronCondorTradePlanFunctionState(); state.ReplayEvents(new IEvent[] { fixture.Receipts[original.CommandId] });
        state.Prepare(replay); Assert.True(state.IsCompleted); Assert.True(state.Matches(replay));
    }
    [Fact]
    public async Task Monitoring_event_preserves_captured_time_and_inputs_when_current_cache_changes()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { Failure = "lost-reply" }; await fixture.Start();
        var source = fixture.Input("Monitoring", 1); Assert.False(await fixture.Deliver(source));
        var first = fixture.PlanRequests.Single(); fixture.Time.Now += TimeSpan.FromHours(1); fixture.Inputs = fixture.Inputs with { PutForwardPrice = 99 }; fixture.Failure = "none";
        Assert.True(await fixture.Deliver(source)); Assert.Equal(first.Fingerprint(), fixture.PlanRequests[1].Fingerprint()); Assert.Single(fixture.Receipts);
    }
}
