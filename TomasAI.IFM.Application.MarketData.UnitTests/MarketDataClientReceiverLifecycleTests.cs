using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class MarketDataClientReceiverLifecycleTests
{
    [Fact]
    public void Confirmed_source_can_be_admitted_while_starting_without_claiming_feed_health()
    {
        var worker=Guid.NewGuid();var generation=Guid.NewGuid();var date=new DateOnly(2026,11,26);
        var status=new MarketDataDatasetStatus("Futures","GLBX.MDP3",worker,generation,123,true,false,"Starting",null,date);
        var snapshot=new MarketDataReadSnapshot(new(false,date,[status]),[],[],[]);
        var registry=new DatasetWorkerAdmissionRegistry();var active=new Dictionary<string,DatasetWorkerAdmission>(StringComparer.Ordinal);
        MarketDataClientReceiver.Sync(registry,active,snapshot,"Futures");Assert.False(registry.TryGet("GLBX.MDP3",out _));
        MarketDataClientReceiver.Sync(registry,active,snapshot,"Futures",includeStarting:true);
        Assert.True(registry.TryGet("GLBX.MDP3",out var admitted));Assert.Equal(generation,admitted.GenerationId);
        Assert.False(snapshot.Status.Ready);Assert.False(status.Healthy);
        var stopped=snapshot with{Status=snapshot.Status with{Datasets=[status with{Running=false,Lifecycle="Starting"}]}};
        MarketDataClientReceiver.Sync(registry,active,stopped,"Futures",includeStarting:true);
        Assert.True(registry.TryGet("GLBX.MDP3",out var retained));Assert.Equal(generation,retained.GenerationId);
        // A provider stop may leave exact original frames at the source relay. Only a real
        // replacement retires their live intake authority; ordinary noncapture behavior still closes it.
        MarketDataClientReceiver.Sync(registry,active,stopped,"Futures");
        Assert.False(registry.TryGet("GLBX.MDP3",out _));
    }

    [Fact(Timeout = 10000)]
    public async Task Receiver_waits_for_actor_bootstrap_before_resolving_a_realtime_producer()
    {
        await using var connections = new NatsConnectionManager();
        await using var connection = new MarketDataHostConnection(connections, Harness.ExplicitBrokerUrl.Value, "receivertest" + Guid.NewGuid().ToString("N"));
        using var cache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache();
        var api = Substitute.For<IMarketDataApi>();
        api.GetBackendReadSnapshotAsync(Arg.Any<CancellationToken>()).Returns(new MarketDataReadSnapshot(new(true, null, []), [], [], []));
        var supervisor = Substitute.For<IActorSupervisor>(); bool ready = false;
        supervisor.IsReady.Returns(_ => Volatile.Read(ref ready));
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        publisher.StartAsync(Arg.Any<CancellationToken>()).Returns(_ => { started.TrySetResult(); return ValueTask.CompletedTask; });
        using var receiver = new MarketDataClientReceiver(connection, api, new(cache), new(), publisher,
            Substitute.For<IMarketDataOperationsRecorder>(), supervisor, NullLogger<MarketDataClientReceiver>.Instance);
        await receiver.StartAsync(default);
        await Task.Delay(100);
        Assert.False(started.Task.IsCompleted);
        Volatile.Write(ref ready, true);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await receiver.StopAsync(stop.Token);
        await publisher.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await publisher.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }
}
