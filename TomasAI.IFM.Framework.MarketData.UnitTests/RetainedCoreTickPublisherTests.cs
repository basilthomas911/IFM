using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Framework.MarketData.UnitTests;

[CollectionDefinition("RetainedCoreItiProof",DisableParallelization=true)]
public sealed class RetainedCoreItiProofCollection;

[Collection("RetainedCoreItiProof")]
public sealed class RetainedCoreTickPublisherTests
{
    [Fact]
    public async Task Retained_core_market_source_requires_positive_original_proof_and_never_calls_live_iti()
    {
        var supervisor=Substitute.For<IActorSupervisor>();var producer=Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var delivered=new TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent,TickDataEntityId>(Arg.Any<ActorSubject>(),Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(),Arg.Any<CancellationToken>())
            .Returns(call=>{delivered.TrySetResult(call.Arg<FuturesMarketPriceUpdatedRealtimeEvent>());return ValueTask.CompletedTask;});
        var source=new FuturesMarketPriceUpdatedRealtimeEvent{Id=Guid.NewGuid(),SourceDataset="GLBX.MDP3",SourceGenerationId=Guid.NewGuid(),Price=new("ES20261218",42,1,AssetTypeId.Futures,new(2026,11,26),null,null)};
        var allowed=false;var proofs=0;var liveCalls=0;
        ValueTask<bool> Proof(FuturesMarketPriceUpdatedRealtimeEvent value,CancellationToken _){Assert.Same(source,value);proofs++;return ValueTask.FromResult(allowed);}
        ValueTask<bool> ReplayProof(FuturesTradeReplayBatchRealtimeEvent _,CancellationToken __)=>ValueTask.FromResult(false);
        ValueTask Live(FuturesMarketPriceUpdatedRealtimeEvent _,CancellationToken __){liveCalls++;return ValueTask.FromException(new InvalidOperationException("Must never resample current inputs."));}
        FuturesItiOrderedIngress.RegisterRecoveryProof(Proof,ReplayProof);FuturesItiOrderedIngress.Register(Live);
        try
        {
            await using var publisher=new TickAggregationEventPublisher(supervisor);await publisher.StartAsync(default);
            await Assert.ThrowsAsync<IOException>(async()=>await publisher.PublishRecoveredAsync(source,default));
            Assert.False(delivered.Task.IsCompleted);allowed=true;
            await publisher.PublishRecoveredAsync(source,default);
            Assert.Same(source,await delivered.Task.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(2,proofs);Assert.Equal(0,liveCalls);
        }
        finally{FuturesItiOrderedIngress.Unregister(Live);FuturesItiOrderedIngress.UnregisterRecoveryProof(Proof,ReplayProof);}
    }
}
