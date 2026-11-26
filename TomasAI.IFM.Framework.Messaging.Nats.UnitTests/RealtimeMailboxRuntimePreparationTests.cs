using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class RealtimeMailboxRuntimePreparationTests
{
    [Fact]
    public async Task Local_preparation_is_bounded_repeatable_and_singleflight_without_broker_or_market_inputs()
    {
        var results = await Task.WhenAll(Enumerable.Range(0,16).Select(_ => RealtimeMailboxRuntimePreparation.PrepareAsync(null)));
        var first=results[0];
        Assert.All(results,result=>Assert.Equal(first,result));
        Assert.Equal(1,first.Assemblies);
        Assert.InRange(first.PreparedMethods,1,4096);
        Assert.Equal(11,first.ControlModels);
        Assert.InRange(first.ControlBytes,1,11*65536);
        Assert.Equal(first,await RealtimeMailboxRuntimePreparation.PrepareAsync(null));
    }
    [Fact]
    public async Task Unrelated_assembly_is_not_prepared()
        => await Assert.ThrowsAsync<ArgumentException>(()=>RealtimeMailboxRuntimePreparation.PrepareAsync(typeof(string)));
    [Fact]
    public async Task Actual_scheduler_assembly_prepares_without_constructing_scheduler_or_business_handlers()
    {
        var result=await RealtimeMailboxRuntimePreparation.PrepareAsync(typeof(TomasAI.IFM.Application.EventModelActor.ActorThreadPoolV2));
        Assert.Equal(2,result.Assemblies);
        Assert.InRange(result.PreparedMethods,1,4096);
        Assert.Equal(11,result.ControlModels);
        Assert.Equal(result,await RealtimeMailboxRuntimePreparation.PrepareAsync(typeof(TomasAI.IFM.Application.EventModelActor.ActorThreadPoolV2)));
    }
    [Fact]
    public async Task Cancelled_caller_does_not_start_or_poison_shared_preparation()
    {
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>RealtimeMailboxRuntimePreparation.PrepareAsync(null,cancelled.Token));
        Assert.Equal(11,(await RealtimeMailboxRuntimePreparation.PrepareAsync(null)).ControlModels);
    }
}
