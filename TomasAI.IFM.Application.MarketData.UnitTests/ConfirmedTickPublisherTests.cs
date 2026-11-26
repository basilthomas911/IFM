using NSubstitute;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
[CollectionDefinition("Confirmed tick source",DisableParallelization=true)]
public sealed class ConfirmedTickSourceCollection;
[Collection("Confirmed tick source")]
public sealed class ConfirmedTickPublisherTests
{
    sealed class Capture(List<string> steps):IRealtimePublicationCapture
    {
        public bool Enabled=>true;public bool Fail;
        public ValueTask<RealtimeCaptureReceipt> CaptureAsync(RealtimeSourcePublication source,CancellationToken token=default)
        {steps.Add("capture");if(Fail)return ValueTask.FromException<RealtimeCaptureReceipt>(new IOException("Capture refused."));return ValueTask.FromResult(new RealtimeCaptureReceipt(source.SourceEventId,new string('A',64),new string('B',64),1,false));}
    }
    static FuturesMarketPriceUpdatedRealtimeEvent Source()=>new()
    {
        Id=Guid.NewGuid(),Subject=new(ActorType.Realtime,FuturesMarketPriceUpdatedRealtimeEvent.Actor,FuturesMarketPriceUpdatedRealtimeEvent.Verb,"ES20261218"),
        EntityId=new("ES20261218",new DateOnly(2026,11,20),AssetTypeId.Futures),
        Price=new("ES20261218",42,1,AssetTypeId.Futures,new DateOnly(2026,11,20),null,null),SourceDataset="GLBX.MDP3",SourceGenerationId=Guid.NewGuid()
    };
    [Fact]
    public async Task Fresh_source_prepares_frozen_input_before_capture_and_releases_only_after_confirmation()
    {
        List<string> steps=[];var capture=new Capture(steps);var supervisor=Substitute.For<IActorSupervisor>();await using var publisher=new TickAggregationEventPublisher(supervisor,capture:capture);
        Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> prepare=(_,_)=>{steps.Add("prepare");return ValueTask.CompletedTask;};
        Func<FuturesMarketPriceUpdatedRealtimeEvent,CancellationToken,ValueTask> release=(_,_)=>{steps.Add("release");return ValueTask.CompletedTask;};
        var previousHealth=FuturesItiOrderedIngress.AllowsEntries;var previousReason=FuturesItiOrderedIngress.StatusReason;
        FuturesItiOrderedIngress.RegisterPreparation(prepare);FuturesItiOrderedIngress.Register(release);
        try
        {
            await publisher.StartAsync();capture.Fail=true;var source=Source();await Assert.ThrowsAsync<IOException>(async()=>await publisher.PublishAsync(source));Assert.Equal(new[]{"prepare","capture"},steps);
            steps.Clear();capture.Fail=false;await publisher.PublishAsync(source);Assert.Equal(new[]{"prepare","capture","release"},steps);
            Assert.Equal(1,publisher.GetSnapshot().Published); // Capture confirmation, never volatile queue admission.
        }
        finally{FuturesItiOrderedIngress.UnregisterPreparation(prepare);FuturesItiOrderedIngress.Unregister(release);FuturesItiOrderedIngress.SetHealth(previousHealth,previousReason);}
    }
}
