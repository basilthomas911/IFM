using MessagePack;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class ConfirmedSourceBatchTests
{
    static DatasetWorkerAdmission Identity()=>new("GLBX.MDP3",new DateOnly(2026,11,20),Guid.NewGuid(),Guid.NewGuid(),1);
    static DatasetPublicationEnvelope Price(DatasetWorkerAdmission identity,long sequence)=>new(){Dataset=identity.Dataset,ValueDate=identity.ValueDate,WorkerInstanceId=identity.WorkerInstanceId,GenerationId=identity.GenerationId,ManifestRevision=identity.ManifestRevision,PublicationSequence=sequence,Kind=DatasetPublicationKind.MarketPrice,
        Payload=MessagePackSerializer.Serialize(new FuturesMarketPriceUpdatedRealtimeEvent{Id=Guid.NewGuid(),Price=new("ES20261218",42,1,AssetTypeId.Futures,identity.ValueDate,null,null)})};
    static (DatasetPublicationIngress Ingress,ITickAggregationEventPublisher Publisher,IDurableTickPublicationBatch Batch,DatasetWorkerAdmissionRegistry Registry) Fixture(DatasetWorkerAdmission identity)
    {
        var registry=new DatasetWorkerAdmissionRegistry();registry.Admit(identity);
        var publisher=Substitute.For<ITickAggregationEventPublisher,IDurableTickPublicationBatch,IRecoveredFuturesItiPublisher>();
        return(new(registry,publisher,Substitute.For<IMarketDataOperationsRecorder>(),evidencePersistedByHost:true,requireDurableCapture:true),publisher,(IDurableTickPublicationBatch)publisher,registry);
    }
    [Fact]
    public async Task Failed_source_group_keeps_first_input_pinned_and_retries_without_advancing_watermark()
    {
        var identity=Identity();var fixture=Fixture(identity);var fail=true;var frames=new[]{Price(identity,1),Price(identity,2)};
        fixture.Batch.PublishBatchAsync(Arg.Any<IReadOnlyList<IEvent>>(),Arg.Any<bool>(),Arg.Any<CancellationToken>()).Returns(_=>fail?ValueTask.FromException(new IOException("Capture refused.")):ValueTask.CompletedTask);
        await Assert.ThrowsAsync<IOException>(async()=>await fixture.Ingress.CaptureSourceBatchAsync(frames));Assert.Equal(0,fixture.Registry.LastConfirmedSequence(identity));
        await Assert.ThrowsAsync<InvalidDataException>(async()=>await fixture.Ingress.CaptureSourceBatchAsync(new[]{Price(identity,3)}));
        fail=false;Assert.All(await fixture.Ingress.CaptureSourceBatchAsync(frames),Assert.True);Assert.Equal(2,fixture.Registry.LastConfirmedSequence(identity));
    }
    [Fact]
    public async Task Lost_receipt_confirmed_prefix_is_replayed_and_new_suffix_remains_fresh()
    {
        var identity=Identity();var fixture=Fixture(identity);var frames=new[]{Price(identity,1),Price(identity,2),Price(identity,3)};List<(int Count,bool Recovered)> calls=[];
        fixture.Batch.PublishBatchAsync(Arg.Any<IReadOnlyList<IEvent>>(),Arg.Any<bool>(),Arg.Any<CancellationToken>()).Returns(call=>{calls.Add((call.Arg<IReadOnlyList<IEvent>>().Count,call.Arg<bool>()));return ValueTask.CompletedTask;});
        await fixture.Ingress.CaptureSourceBatchAsync(frames.Take(2).ToArray());calls.Clear();
        var live=await fixture.Ingress.CaptureSourceBatchAsync(frames);Assert.Equal(new[]{false,false,true},live);Assert.Equal(new[]{(2,true),(1,false)},calls);Assert.Equal(3,fixture.Registry.LastConfirmedSequence(identity));
    }
    [Fact]
    public async Task Replay_controls_are_single_source_barriers_and_mixed_generations_are_rejected()
    {
        var identity=Identity();var fixture=Fixture(identity);List<string> order=[];
        fixture.Batch.PublishBatchAsync(Arg.Any<IReadOnlyList<IEvent>>(),Arg.Any<bool>(),Arg.Any<CancellationToken>()).Returns(call=>{order.Add("group:"+call.Arg<IReadOnlyList<IEvent>>().Count);return ValueTask.CompletedTask;});
        fixture.Publisher.PublishAsync(Arg.Any<FuturesTradeReplayBatchRealtimeEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{order.Add("replay");return ValueTask.CompletedTask;});
        var first=Price(identity,1);var replay=Price(identity,2) with{Kind=DatasetPublicationKind.TradeReplayBatch,Payload=MessagePackSerializer.Serialize(new FuturesTradeReplayBatchRealtimeEvent{Id=Guid.NewGuid(),EntityId=new("ES20261218",identity.ValueDate,AssetTypeId.Futures),RecoveryGenerationId=Guid.NewGuid(),LiveStreamEpochId=Guid.NewGuid(),IsFirstBatch=true,IsFinalBatch=true})};
        Assert.All(await fixture.Ingress.CaptureSourceBatchAsync(new[]{first,replay,Price(identity,3)}),Assert.True);Assert.Equal(new[]{"group:1","replay","group:1"},order);
        await Assert.ThrowsAsync<InvalidDataException>(async()=>await fixture.Ingress.CaptureSourceBatchAsync(new[]{Price(identity,4),Price(identity with{GenerationId=Guid.NewGuid()},5)}));
    }
}
