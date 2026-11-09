using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport.ResidentActors;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

[CollectionDefinition("Resident actor pilots", DisableParallelization = true)]
public sealed class ResidentActorPilotCollection : ICollectionFixture<ResidentActorPilotFixture>;

public sealed class ResidentActorPilotFixture : IAsyncLifetime
{
    public ResidentPilotDatabase Database { get; } = new();
    int key;
    public int NextKey() => Interlocked.Increment(ref key);
    public Task InitializeAsync() => Database.InitializeAsync();
    public async Task DisposeAsync() => await Database.DisposeAsync();
}

/// <summary>Actual actor, audit, snapshot, transaction and projection paths on disposable PostgreSQL.</summary>
[Collection("Resident actor pilots")]
public sealed class ResidentActorPilotTests(ResidentActorPilotFixture fixture)
{
    public static IEnumerable<object[]> Pilots => Enum.GetValues<ResidentPilotKind>().Select(kind => new object[] { kind });
    public static IEnumerable<object[]> Modes => Pilots.SelectMany(row => new[] { false, true }.Select(mode => new[] { row[0], mode }));

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Four_updates_commit_before_reply_and_residency_removes_only_repeated_state_loads(ResidentPilotKind kind, bool enabled)
    {
        var db = fixture.Database;
        await using var session = await db.CreateSessionAsync(kind, enabled);
        var key = fixture.NextKey();
        for (var revision = 1; revision <= 4; revision++)
        {
            var request = session.Request(key, revision);
            long atReply = -1;
            var message = await session.RunAsync(request, beforeReply: async () => atReply = await db.EventCountAsync(request.StreamId));
            Assert.True(message.Result?.Success, message.Result?.ErrorMessage);
            Assert.Equal(revision, atReply);
            Assert.Equal(revision, await db.Events.GetCommittedStreamVersionAsync(request.StreamId));
        }
        Assert.Equal(enabled ? 1 : 4, session.Loads);
        Assert.Equal(enabled ? 3 : 0, session.Statistics.Hits);
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Failed_commit_rolls_back_source_and_projection_then_reloads_committed_state(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        await using var session = await db.CreateSessionAsync(kind, true);
        var key = fixture.NextKey();
        var first = session.Request(key, 1);
        Assert.True((await session.RunAsync(first)).Result?.Success);
        db.Transactions.FailNextCommit = true;
        var failed = await session.RunAsync(session.Request(key, 2));
        Assert.False(failed.Result?.Success, failed.Result?.ErrorMessage);
        Assert.Equal(1, await db.EventCountAsync(first.StreamId));
        Assert.True((await session.RunAsync(session.Request(key, 3))).Result?.Success);
        Assert.Equal(2, await db.EventCountAsync(first.StreamId));
        Assert.Equal(2, session.Loads);
        Assert.True(session.Statistics.Invalidations >= 1);
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Recreated_actor_loads_the_latest_full_snapshot_and_continues_without_duplicate_events(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        var key = fixture.NextKey();
        string stream;
        await using (var first = await db.CreateSessionAsync(kind, true))
        {
            var request = first.Request(key, 1);
            stream = request.StreamId;
            Assert.True((await first.RunAsync(request)).Result?.Success);
            Assert.True((await first.RunAsync(first.Request(key, 2))).Result?.Success);
            Assert.Equal(1, first.Loads);
        }
        await using var restarted = await db.CreateSessionAsync(kind, true);
        var next = await restarted.RunAsync(restarted.Request(key, 3));
        Assert.True(next.Result?.Success, next.Result?.ErrorMessage);
        Assert.Equal(1, restarted.Loads);
        Assert.Equal(3, await db.EventCountAsync(stream));
        Assert.Equal(3, await db.Events.GetCommittedStreamVersionAsync(stream));
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Duplicate_request_replays_success_without_another_source_append(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        await using var session = await db.CreateSessionAsync(kind, true);
        var request = session.Request(fixture.NextKey(), 1);
        Assert.True((await session.RunAsync(request)).Result?.Success);
        var replay = await session.RunAsync(request);
        Assert.True(replay.Result?.Success, replay.Result?.ErrorMessage);
        Assert.Equal(1, await db.EventCountAsync(request.StreamId));
    }

    [Theory]
    [InlineData(ResidentPilotKind.Vwap)]
    [InlineData(ResidentPilotKind.MarketOutlook)]
    public async Task Stale_cached_no_change_cannot_acknowledge_after_another_writer_advanced_the_stream(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        var key = fixture.NextKey();
        await using var cached = await db.CreateSessionAsync(kind, true);
        await using var external = await db.CreateSessionAsync(kind, false);
        var original = cached.Request(key, 1);
        Assert.True((await cached.RunAsync(original)).Result?.Success);
        Assert.True((await external.RunAsync(external.Request(key, 2))).Result?.Success);
        var stale = await cached.RunAsync(cached.Request(key, 1));
        Assert.False(stale.Result?.Success);
        Assert.Equal(2, await db.EventCountAsync(original.StreamId));
        Assert.True((await cached.RunAsync(cached.Request(key, 3))).Result?.Success);
        Assert.Equal(3, await db.EventCountAsync(original.StreamId));
        Assert.Equal(2, cached.Loads);
    }

    [Theory]
    [InlineData(ResidentPilotKind.FuturesPlan)]
    [InlineData(ResidentPilotKind.VerticalSpreadPlan)]
    [InlineData(ResidentPilotKind.IronCondorPlan)]
    public async Task Cached_completion_checks_the_head_before_replaying_after_another_writer(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        var key = fixture.NextKey();
        await using var cached = await db.CreateSessionAsync(kind, true);
        await using var external = await db.CreateSessionAsync(kind, false);
        var original = cached.Request(key, 1);
        Assert.True((await cached.RunAsync(original)).Result?.Success);
        Assert.True((await external.RunAsync(external.Request(key, 2))).Result?.Success);
        var stale = await cached.RunAsync(original);
        Assert.False(stale.Result?.Success);
        Assert.Equal(2, await db.EventCountAsync(original.StreamId));
        Assert.Equal(2, cached.Loads);
        Assert.True((await cached.RunAsync(cached.Request(key, 3))).Result?.Success);
        Assert.Equal(3, await db.EventCountAsync(original.StreamId));
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Full_cache_falls_back_and_all_streams_remain_reconstructable(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        await using var session = await db.CreateSessionAsync(kind, true, capacity: 2);
        var keys = Enumerable.Range(0, 5).Select(_ => fixture.NextKey()).ToArray();
        foreach (var revision in new[] { 1, 2 })
        foreach (var key in keys)
        {
            var request = session.Request(key, revision);
            var reply = await session.RunAsync(request);
            Assert.True(reply.Result?.Success, reply.Result?.ErrorMessage);
            Assert.Equal(revision, await db.EventCountAsync(request.StreamId));
            Assert.True(session.Statistics.Entries <= 2);
        }
        Assert.True(session.Statistics.Evictions > 0);
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Real_NATS_request_reply_reuses_state_then_recovers_after_actor_recreation(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var client = new NatsClient(db.NatsUrl);
        var key = fixture.NextKey();
        for (var lifetime = 0; lifetime < 2; lifetime++)
        {
            await using var session = await db.CreateSessionAsync(kind, true);
            var first = session.Request(key, lifetime * 2 + 1);
            await using var sub = await client.Connection.SubscribeCoreAsync<byte[]>(first.Subject.ToString(), cancellationToken: deadline.Token);
            await client.Connection.PingAsync(deadline.Token);
            using var pumpStop = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var pump = Task.Run(async () =>
            {
                try
                {
                    await foreach (var message in sub.Msgs.ReadAllAsync(pumpStop.Token))
                    {
                        using var adapter = new NatsActorMessage(message);
                        await session.DispatchAsync(adapter, pumpStop.Token);
                    }
                }
                catch (OperationCanceledException) when (pumpStop.IsCancellationRequested) { }
            });
            foreach (var revision in new[] { lifetime * 2 + 1, lifetime * 2 + 2 })
            {
                var request = session.Request(key, revision);
                var bytes = Serialize(request);
                var reply = await client.RequestAsync<byte[], ServiceResult>(request.Subject.ToString(), bytes,
                    replySerializer: NatsMessagePackSerializer<ServiceResult>.Default, cancellationToken: deadline.Token);
                Assert.True(reply.Data?.Success, reply.Data?.ErrorMessage);
            }
            await pumpStop.CancelAsync();
            await pump;
            Assert.Equal(1, session.Loads);
            Assert.Equal((lifetime + 1) * 2, await db.EventCountAsync(first.StreamId));
        }
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Lost_success_reply_after_commit_recovers_by_identity_without_duplicate_append(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        ICommand request;
        await using (var session = await db.CreateSessionAsync(kind, true))
        {
            request = session.Request(fixture.NextKey(), 1);
            try
            {
                await session.RunAsync(request, beforeReply: () => ValueTask.FromException(new IOException("Injected lost reply after commit.")));
            }
            catch (IOException) { }
            Assert.Equal(1, await db.EventCountAsync(request.StreamId));
        }
        await using var restarted = await db.CreateSessionAsync(kind, true);
        var retry = await restarted.RunAsync(request);
        Assert.True(retry.Result?.Success, retry.Result?.ErrorMessage);
        Assert.Equal(1, await db.EventCountAsync(request.StreamId));
        Assert.Equal(1, await db.Events.GetCommittedStreamVersionAsync(request.StreamId));
    }

    [Theory]
    [MemberData(nameof(Pilots))]
    public async Task Changed_request_after_external_writer_rejects_stale_version_then_reloads(ResidentPilotKind kind)
    {
        var db = fixture.Database;
        await using var cached = await db.CreateSessionAsync(kind, true);
        await using var external = await db.CreateSessionAsync(kind, false);
        var key = fixture.NextKey();
        var first = cached.Request(key, 1);
        Assert.True((await cached.RunAsync(first)).Result?.Success);
        Assert.True((await external.RunAsync(external.Request(key, 2))).Result?.Success);
        var conflict = await cached.RunAsync(cached.Request(key, 3));
        Assert.False(conflict.Result?.Success);
        Assert.Equal(2, await db.EventCountAsync(first.StreamId));
        var next = await cached.RunAsync(cached.Request(key, 4));
        Assert.True(next.Result?.Success, next.Result?.ErrorMessage);
        Assert.Equal(3, await db.EventCountAsync(first.StreamId));
        Assert.True(cached.Statistics.Invalidations > 0);
    }

    static byte[] Serialize(ICommand request)
    {
        var codec = new NatsMessagePackDataSerializer();
        return request switch
        {
            UpdateFuturesVwapSignalCommand command => codec.Serialize(command),
            InsertMarketOutlookSnapshotCommand command => codec.Serialize(command),
            UpdateFuturesTradePlanCommand command => codec.Serialize(command),
            UpdateVerticalSpreadTradePlanCommand command => codec.Serialize(command),
            UpdateIronCondorTradePlanCommand command => codec.Serialize(command),
            _ => throw new InvalidOperationException("Unknown pilot command.")
        };
    }
}
