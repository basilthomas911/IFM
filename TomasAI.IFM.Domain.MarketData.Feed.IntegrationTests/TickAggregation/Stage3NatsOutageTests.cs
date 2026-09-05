using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.TickAggregation;

/// <summary>Opt-in: owns a disposable cached-image broker, never the developer's shared NATS service.</summary>
public sealed class Stage3NatsOutageTests
{
    [IsolatedNatsFact]
    public async Task Actual_core_transport_outage_recovers_without_replaying_failed_session_backlog()
    {
        // Pin the chosen loopback port: Docker's automatic random port can change on restart.
        int port;
        using (var reservation = new TcpListener(IPAddress.Loopback, 0))
        {
            reservation.Start();
            port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        }
        var id = (await Docker("run", "-d", "--pull", "never", "--label", "ifm.test=stage3-acceptance",
            "-p", $"127.0.0.1:{port}:4222", "nats:2.12.0-alpine")).Trim();
        Assert.Matches("^[a-f0-9]{64}$", id);
        try
        {
            var address = (await Docker("port", id, "4222/tcp")).Trim();
            Assert.Matches("^127[.]0[.]0[.]1:[0-9]+$", address);
            var url = $"nats://{address}";
            await using var connections = new NatsConnectionManager();
            var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance, connections);
            var mailbox = new ActorMailboxId(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor);
            await producer.StartAsync(mailbox);
            var sendingClient = await connections.GetClientAsync(url);
            await using var observer = new NatsClient(url);
            await observer.ConnectAsync();
            await using var subscription = await observer.Connection.SubscribeCoreAsync<byte[]>(">");
            await observer.Connection.PingAsync();
            var supervisor = Substitute.For<IActorSupervisor>();
            supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
            await using var publisher = new TickAggregationEventPublisher(supervisor,
                policy: new() { Capacity = 4, SendTimeout = TimeSpan.FromMilliseconds(500),
                    CancellationGracePeriod = TimeSpan.FromSeconds(1) });
            try
            {
                await publisher.StartAsync();
                var before = Price("before");
                await publisher.PublishAsync(before);
                Assert.Equal(before.Subject.ToString(), (await subscription.Msgs.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Subject);
                await Docker("stop", "-t", "1", id);
                await Until(() => sendingClient.Connection.ConnectionState != NatsConnectionState.Open);
                var outage = Price("outage");
                var backlog = Price("backlog");
                await publisher.PublishAsync(outage);
                await publisher.PublishAsync(backlog);
                await Until(() => publisher.GetSnapshot().Faulted);
                Assert.False(publisher.GetSnapshot().UncontainedSend);
                Assert.Equal(0, publisher.GetSnapshot().Depth);
                await Docker("start", id);
                await Until(() => sendingClient.Connection.ConnectionState == NatsConnectionState.Open
                    && observer.Connection.ConnectionState == NatsConnectionState.Open);
                await observer.Connection.PingAsync();
                await Until(() => publisher.GetSnapshot().CanRecover);
                await publisher.StartAsync();
                var fresh = Price("fresh");
                await publisher.PublishAsync(fresh);
                Assert.Equal(fresh.Subject.ToString(), (await subscription.Msgs.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).Subject);
                using var quiet = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => subscription.Msgs.ReadAsync(quiet.Token).AsTask());
                Assert.Equal(2, publisher.GetSnapshot().Published);
            }
            finally { await publisher.StopAsync(); await producer.StopAsync(); }
        }
        finally { await Docker("rm", "-f", id); }
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Price(string label)
    {
        var entity = new TickDataEntityId($"S3{label}", new DateOnly(2026, 9, 4), AssetTypeId.Futures);
        return new()
        {
            Subject = new ActorSubject(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor,
                FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entity, AggregateId = entity.Format(),
            EventSource = nameof(Stage3NatsOutageTests), ReceivedOn = DateTime.UtcNow,
            Price = new(entity.ContractId, 42, 1, AssetTypeId.Futures, entity.ValueDate, null, null)
        };
    }

    static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!condition()) await Task.Delay(25, timeout.Token);
    }

    static async Task<string> Docker(params string[] arguments)
    {
        var start = new ProcessStartInfo("docker")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        if (process.ExitCode != 0) throw new InvalidOperationException($"Isolated qualification broker command failed: {await error}");
        return await output;
    }
}

public sealed class IsolatedNatsFactAttribute : FactAttribute
{
    public IsolatedNatsFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_STAGE3_ISOLATED_NATS") != "1")
            Skip = "Set IFM_STAGE3_ISOLATED_NATS=1 to create/stop/restart/remove an isolated cached-image test broker.";
    }
}
