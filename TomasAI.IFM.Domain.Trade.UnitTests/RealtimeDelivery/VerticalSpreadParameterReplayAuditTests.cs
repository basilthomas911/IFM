using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class VerticalSpreadParameterReplayAuditTests
{
    [Fact]
    public async Task Parameter_version_change_reuses_original_frozen_plan_after_lost_reply()
    {
        await using var fixture=new VerticalSpreadPositionDeliveryFixture(false){Failure="lost-reply"};await fixture.Start();
        var source=VerticalSpreadPositionDeliveryFixture.Input(1);Assert.False(await fixture.Deliver(source));var original=Assert.Single(fixture.Requests);
        fixture.Parameters=fixture.Parameters with{Version=fixture.Parameters.Version+1,ProfitTarget=fixture.Parameters.ProfitTarget+1};fixture.Failure="none";
        Assert.True(await fixture.Deliver(source));var retry=fixture.Requests[1];
        Assert.Equal(original.CommandId,retry.CommandId);Assert.Equal(original.Fingerprint(),retry.Fingerprint());
        var state=new VerticalSpreadTradePlanFunctionState();state.ReplayEvents(new IEvent[]{fixture.Receipts[original.CommandId]});state.Prepare(retry);
        Assert.True(state.IsCompleted);Assert.True(state.Matches(retry));Assert.Single(fixture.AcceptedExits);
    }
}
