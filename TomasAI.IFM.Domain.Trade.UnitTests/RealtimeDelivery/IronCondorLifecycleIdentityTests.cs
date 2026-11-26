using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class IronCondorLifecycleIdentityTests
{
    static ProjectionExecutionContext Execution(long source)=>new("IronCondorPositionEventProjector",source,1,
        new("IronCondorPositionEventProjector",source,EventProjectorEffectKind.TargetProjection),Guid.NewGuid(),EventProjectionIdempotencyStrategy.NaturalKeyMutation,default);
    [Fact]
    public void Legacy_source_has_stable_notification_identity_without_mutation_or_retry_restamping()
    {
        var at=new DateTime(2026,11,25,14,30,0,DateTimeKind.Utc);var source=new IronCondorPositionChangedEvent{EventId=17,ReceivedOn=at};
        var original=MessagePackSerializer.Serialize(source);
        var first=IronCondorPositionEventProjector.LifecyclePublication(source,Execution(17));
        var retry=IronCondorPositionEventProjector.LifecyclePublication(source,Execution(17));
        Assert.NotEqual(Guid.Empty,first.Id);Assert.Equal(first.Id,retry.Id);Assert.Equal(at,retry.ReceivedOn);
        Assert.Equal(original,MessagePackSerializer.Serialize(source));Assert.Equal(Guid.Empty,source.Id);
        Assert.Null(MessageOrigins.Get(first));Assert.Null(MessageOrigins.ForPublication(first,TimeProvider.System));
        Assert.NotEqual(first.Id,IronCondorPositionEventProjector.LifecyclePublication(source with{EventId=18},Execution(18)).Id);
    }
    [Fact]
    public void Existing_identity_and_origin_are_preserved_and_mismatched_source_is_rejected()
    {
        var source=new IronCondorPositionChangedEvent{Id=Guid.NewGuid(),EventId=17};
        var origin=new MessageOrigin(new DateTimeOffset(2026,11,25,14,30,0,TimeSpan.Zero),"qualification");MessageOrigins.Restore(source,origin);
        var result=IronCondorPositionEventProjector.LifecyclePublication(source,Execution(17));
        Assert.Equal(source.Id,result.Id);Assert.Equal(origin,MessageOrigins.Get(result));
        Assert.Throws<InvalidDataException>(()=>IronCondorPositionEventProjector.LifecyclePublication(source,Execution(18)));
    }
}
