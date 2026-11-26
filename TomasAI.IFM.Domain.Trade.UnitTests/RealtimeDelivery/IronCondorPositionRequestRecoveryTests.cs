using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class IronCondorPositionRequestRecoveryTests
{
    static string DirectoryForTest() => Path.Combine(Path.GetTempPath(), "ifm-condor-request-tests", Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("Position", "lost-reply")]
    [InlineData("Monitoring", "lost-reply")]
    [InlineData("Position", "publication")]
    [InlineData("Monitoring", "publication")]
    public async Task Cold_replay_uses_original_time_inputs_parameters_and_forward_response(string kind, string failure)
    {
        var path = DirectoryForTest();
        TomasAI.IFM.Shared.EventSourcing.IEvent source;
        UpdateIronCondorTradePlanCommand original;
        IronCondorTradePlanUpdatedEvent committed;
        await using (var first = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path) { Failure = failure })
        {
            await first.Start(); source = first.Input(kind, 1); Assert.False(await first.Deliver(source));
            original = first.PlanRequests.Single(); committed = first.Receipts[original.CommandId];
        }
        await using var next = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path);
        next.Time.Now += TimeSpan.FromDays(1); next.Inputs = next.Inputs with { PutForwardPrice = 99 };
        next.InputsAvailable = false; next.Parameters = new() { Version = 2, MaximumLoss = 2000 };
        next.ForwardFails = true; next.Receipts.Add(original.CommandId, committed); await next.Start();
        Assert.True(await next.Deliver(source)); Assert.Empty(next.ForwardRequests);
        Assert.Equal(MessagePackSerializer.Serialize(original), MessagePackSerializer.Serialize(next.PlanRequests.Single()));
        next.InputsAvailable = true; next.ForwardFails = false;
        Assert.True(await next.Deliver(next.Input(kind, 2)));
        Assert.Equal(new long[] { 1, 2 }, next.PublishedPlans); Assert.Equal(2, next.Receipts.Count);
    }
    [Fact]
    public async Task Frozen_forward_failure_cannot_be_replaced_by_success_on_plan_retry()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false) { ForwardFails = true, Failure = "throw" };
        await fixture.Start(); var source = fixture.Input("Position", 1); Assert.False(await fixture.Deliver(source));
        var original = fixture.PlanRequests.Single(); Assert.NotNull(original.ForwardPriceFailure);
        fixture.ForwardFails = false; fixture.Failure = "none"; fixture.Time.Now += TimeSpan.FromSeconds(3);
        Assert.True(await fixture.Deliver(source)); Assert.Single(fixture.ForwardRequests);
        Assert.Equal(original.Fingerprint(), fixture.PlanRequests[1].Fingerprint());
    }
    [Fact]
    public async Task Capture_write_failure_prevents_any_function_dispatch_and_fails_delivery()
    {
        var path = DirectoryForTest();
        await using var fixture = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path); await fixture.Start();
        var source = fixture.Input("Position", 1);
        Directory.CreateDirectory(Path.Combine(path, "pending", source.Id.ToString("N") + ".icr.tmp"));
        Assert.False(await fixture.Deliver(source)); Assert.Empty(fixture.ForwardRequests); Assert.Empty(fixture.PlanRequests);
    }
    [Fact]
    public async Task Plan_capture_write_failure_prevents_plan_dispatch_and_replays_original_forward_request()
    {
        var path = DirectoryForTest();
        await using var fixture = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path); await fixture.Start();
        var source = fixture.Input("Position", 1);
        var temporary = Path.Combine(path, "pending", source.Id.ToString("N") + ".icr.tmp");
        fixture.BeforeForward = () => Directory.CreateDirectory(temporary);
        Assert.False(await fixture.Deliver(source)); Assert.Single(fixture.ForwardRequests); Assert.Empty(fixture.PlanRequests);
        Directory.Delete(temporary); fixture.BeforeForward = null;
        fixture.Time.Now += TimeSpan.FromMinutes(1); fixture.Inputs = fixture.Inputs with { PutForwardPrice = 99 };
        Assert.True(await fixture.Deliver(source));
        Assert.Equal(fixture.ForwardRequests[0].Fingerprint(), fixture.ForwardRequests[1].Fingerprint());
        Assert.Single(fixture.PlanRequests);
        Assert.Equal(5, fixture.PlanRequests[0].IronCondorTradePlanInputs!.PutForwardPrice);
    }
    [Fact]
    public async Task Corrupt_capture_blocks_cold_recovery_instead_of_recapturing()
    {
        var path = DirectoryForTest();
        await using (var first = new IronCondorPositionDeliveryFixture(false, recoveryDirectory: path) { Failure = "lost-reply" })
        { await first.Start(); Assert.False(await first.Deliver(first.Input("Position", 1))); }
        var file = Directory.GetFiles(path, "*.icr", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(file); bytes[^1] ^= 1; File.WriteAllBytes(file, bytes);
        Assert.Throws<InvalidDataException>(() => new IronCondorPositionRequests(path));
    }
    [Fact]
    public async Task Same_source_id_with_changed_position_is_rejected()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false); await fixture.Start();
        var original = (IronCondorPositionChangedEvent)fixture.Input("Position", 1); Assert.True(await fixture.Deliver(original));
        Assert.False(await fixture.Deliver(original with { PositionSnapshot = IronCondorPositionDeliveryFixture.Position(2) }));
        Assert.Single(fixture.PlanRequests);
    }
    [Fact]
    public async Task Same_monitoring_id_with_changed_event_inputs_is_rejected()
    {
        await using var fixture = new IronCondorPositionDeliveryFixture(false); await fixture.Start();
        var original = (IronCondorMonitoringInputsChangedEvent)fixture.Input("Monitoring", 1); Assert.True(await fixture.Deliver(original));
        Assert.False(await fixture.Deliver(original with { IronCondorTradePlanInputs = original.IronCondorTradePlanInputs with { PutForwardPrice = 99 } }));
        Assert.Single(fixture.PlanRequests);
    }
    [Fact]
    public void Journal_has_one_process_owner()
    {
        var path = DirectoryForTest(); using var owner = new IronCondorPositionRequests(path);
        Assert.Throws<IOException>(() => new IronCondorPositionRequests(path));
    }
}
