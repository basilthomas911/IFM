using TomasAI.IFM.Shared.EventModelActor;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class PositionPlanRestartTests
{
    [Fact]
    public async Task Cold_restart_reuses_original_full_versioned_vertical_request_after_lost_reply()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ifm-plan-restart-"+Guid.NewGuid().ToString("N"));
        UpdateVerticalSpreadTradePlanCommand original;VerticalSpreadTradePlanUpdatedEvent receipt;
        try
        {
            await using(var first=new VerticalSpreadPositionDeliveryFixture(false,positionIntentDirectory:directory){Failure="lost-reply"})
            {
                await first.Start();Assert.False(await first.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1)));
                original=Assert.Single(first.Requests);receipt=first.Receipts[original.CommandId];
            }
            await using(var recovered=new VerticalSpreadPositionDeliveryFixture(false,positionIntentDirectory:directory))
            {
                recovered.Parameters=recovered.Parameters with{Version=original.Parameters.Version+1,ProfitTarget=original.Parameters.ProfitTarget+2};
                recovered.Time.Now+=TimeSpan.FromDays(1);recovered.Receipts.Add(original.CommandId,receipt);
                await recovered.Start();var replay=Assert.Single(recovered.Requests);
                Assert.Equal(MessagePackSerializer.Serialize(original),MessagePackSerializer.Serialize(replay));
                Assert.Equal(MessageOrigins.Get(original),MessageOrigins.Get(replay));
                var exit=Assert.Single(recovered.AcceptedExits);
                Assert.Equal(MessageOrigins.Get(original)!.OriginatedAtUtc,MessageOrigins.Get(exit)!.OriginatedAtUtc);
            }
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    [Fact]
    public async Task Cold_restart_publishes_frozen_exit_without_recalculating_plan()
    {
        var directory=Path.Combine(Path.GetTempPath(),"ifm-plan-output-restart-"+Guid.NewGuid().ToString("N"));
        byte[] output;
        try
        {
            await using(var first=new VerticalSpreadPositionDeliveryFixture(false,positionIntentDirectory:directory){Failure="exit-send"})
            {
                await first.Start();Assert.False(await first.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1)));
                output=MessagePackSerializer.Serialize(Assert.Single(first.ExitAttempts));
            }
            await using(var recovered=new VerticalSpreadPositionDeliveryFixture(false,positionIntentDirectory:directory))
            {
                recovered.Parameters=recovered.Parameters with{Version=recovered.Parameters.Version+1};
                await recovered.Start();Assert.Empty(recovered.Requests);
                Assert.Equal(output,MessagePackSerializer.Serialize(Assert.Single(recovered.AcceptedExits)));
            }
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
