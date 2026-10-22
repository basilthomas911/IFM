using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class MarketDataClientReceiverLifecycleTests
{
    [Fact(Timeout = 10000)]
    public async Task Receiver_waits_for_actor_bootstrap_before_resolving_a_realtime_producer()
    {
        await using var connections = new NatsConnectionManager();
        await using var connection = new MarketDataHostConnection(connections, "nats://127.0.0.1:4222", "receivertest" + Guid.NewGuid().ToString("N"));
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
