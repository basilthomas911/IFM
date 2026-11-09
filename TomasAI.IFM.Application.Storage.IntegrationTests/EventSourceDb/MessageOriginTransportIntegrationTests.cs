using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class MessageOriginTransportIntegrationTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Real_NATS_preserves_origin_across_producer_restart_and_legacy_forwarding()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = new NatsClient(fixture.NatsUrl);
        var subject = new ActorSubject(ActorType.Realtime, "OriginVerification", "Changed", Guid.NewGuid().ToString("N"));
        await using var subscription = await client.Connection.SubscribeCoreAsync<byte[]>(subject.ToString(), cancellationToken: timeout.Token);
        await client.Connection.PingAsync(timeout.Token);
        var clock = new ControlledClock();
        var source = new DomainEvent { Subject = subject };
        var expected = MessageOrigins.Capture(source, clock, "test-business");
        for (var lifetime = 0; lifetime < 2; lifetime++)
        {
            await using var producer = new NatsActorProducer(new NatsProducerOptions { Url = fixture.NatsUrl }, NullLogger.Instance, timeProvider: clock);
            await producer.StartAsync(new(ActorType.Realtime, "OriginVerification"), timeout.Token);
            await producer.SendAsync(subject, source);
            var received = await subscription.Msgs.ReadAsync(timeout.Token);
            using var adapter = new NatsActorMessage(received, clock);
            source = Assert.IsType<DomainEvent>(adapter.AsEvent<DomainEvent>());
            Assert.Equal(expected, adapter.Origin);
            Assert.Equal(expected, MessageOrigins.Get(source));
            Assert.Equal(expected.OriginatedAtUtc.UtcDateTime, source.ReceivedOn);
            clock.Now = clock.Now.AddDays(1);
        }
        var legacy = new DomainEvent { Subject = subject };
        MessageOrigins.Restore(legacy, null);
        await using var forwarding = new NatsActorProducer(new NatsProducerOptions { Url = fixture.NatsUrl }, NullLogger.Instance, timeProvider: clock);
        await forwarding.StartAsync(new(ActorType.Realtime, "OriginVerification"), timeout.Token);
        await forwarding.SendAsync(subject, legacy);
        var forwarded = await subscription.Msgs.ReadAsync(timeout.Token);
        Assert.Null(MessageOriginHeaders.Read(forwarded.Headers));
    }

    [Fact]
    public async Task New_reply_uses_its_own_clock_identity_instead_of_copying_the_callers_domain()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var client = new NatsClient(fixture.NatsUrl);
        var subject = new ActorSubject(ActorType.Realtime, "ReplyOriginVerification", "Get", Guid.NewGuid().ToString("N"));
        await using var subscription = await client.Connection.SubscribeCoreAsync<byte[]>(subject.ToString(), cancellationToken: timeout.Token);
        await client.Connection.PingAsync(timeout.Token);
        var caller = new MessageOrigin(DateTimeOffset.UtcNow, "control");
        var pending = client.RequestAsync<byte[], ServiceResult<GuidResult>>(subject.ToString(), new byte[] { 0xc0 },
            headers: MessageOriginHeaders.Add(null, caller),
            replySerializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<ServiceResult<GuidResult>>.Default,
            cancellationToken: timeout.Token).AsTask();
        var request = await subscription.Msgs.ReadAsync(timeout.Token);
        var clock = new ControlledClock();
        using var adapter = new NatsActorMessage(request, clock);
        await adapter.ReplyAsync<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(Guid.NewGuid())));
        var reply = await pending;
        var origin = MessageOriginHeaders.Read(reply.Headers);
        Assert.Equal(clock.ClockDomainId, origin!.ClockDomainId);
        Assert.Equal(clock.GetUtcNow(), origin.OriginatedAtUtc);
        Assert.NotEqual(caller, origin);
        Assert.True(reply.Data!.Success);
    }

    [Fact]
    public async Task Repeated_RPCs_with_shared_empty_values_keep_distinct_envelope_origins_without_claiming_value_identity()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var manager = new NatsConnectionManager();
        var clock = new ControlledClock();
        await using var connection = new TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostConnection(
            manager, fixture.NatsUrl, "originrpc" + Guid.NewGuid().ToString("N"), clock);
        var client = await connection.GetClientAsync(timeout.Token);
        const string path = "api/EmptyOriginVerification";
        await using var subscription = await client.Connection.SubscribeCoreAsync<
            TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostRequest>(connection.ControlSubject(path),
            serializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<
                TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostRequest>.Default,
            cancellationToken: timeout.Token);
        await client.PingAsync(timeout.Token);
        var origins = new List<MessageOrigin?>();
        var server = Task.Run(async () =>
        {
            for (var index = 0; index < 2; index++)
            {
                var request = await subscription.Msgs.ReadAsync(timeout.Token);
                origins.Add(MessageOriginHeaders.Read(request.Headers));
                var replyOrigin = new MessageOrigin(clock.Now.AddYears(1), "rpc-reply");
                await request.ReplyAsync(new TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostReply(
                    TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostConnection.Encode(Array.Empty<int>()), null),
                    headers: MessageOriginHeaders.Add(null, replyOrigin),
                    serializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<
                        TomasAI.IFM.Application.MarketData.Databento.Workers.MarketDataHostReply>.Default,
                    cancellationToken: timeout.Token);
            }
        }, timeout.Token);
        var firstTime = clock.Now;
        Assert.Empty(await connection.PostAsync<int[]>(path, Array.Empty<object>(), timeout.Token));
        clock.Now = clock.Now.AddDays(1);
        Assert.Empty(await connection.PostAsync<int[]>(path, Array.Empty<object>(), timeout.Token));
        await server.WaitAsync(timeout.Token);
        Assert.Equal(firstTime, origins[0]!.OriginatedAtUtc);
        Assert.Equal(clock.Now, origins[1]!.OriginatedAtUtc);
        Assert.Null(MessageOrigins.Get(Array.Empty<object>()));
        Assert.Null(MessageOrigins.Get(Array.Empty<int>()));
    }

    sealed class ControlledClock : TimeProvider, TomasAI.IFM.Shared.Time.IMessageClockIdentity
    {
        public string ClockDomainId => "integration-business";
        public Guid? RunId => null;
        public Guid? Generation => null;
        public DateTimeOffset Now { get; set; } = new(2025, 10, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
