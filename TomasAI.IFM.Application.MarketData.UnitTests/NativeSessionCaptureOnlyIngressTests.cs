using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class NativeSessionCaptureOnlyIngressTests
{
    [Fact]
    public async Task Retained_Core_market_source_uses_original_recovery_route_without_admitting_a_live_generation()
    {
        var registry=new DatasetWorkerAdmissionRegistry();
        var publisher=Substitute.For<TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.ITickAggregationEventPublisher,
            TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.IRecoveredFuturesItiPublisher>();
        var ingress=new DatasetPublicationIngress(registry,publisher,Substitute.For<TomasAI.IFM.Application.MarketData.MarketOutlook.IMarketDataOperationsRecorder>(),evidencePersistedByHost:true);
        var original=new TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent
        {
            Id=Guid.NewGuid(),Price=new("ES20261218",42,1,TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,new(2026,11,26),null,null)
        };
        var frame=Frame(DatasetPublicationKind.MarketPrice,17)with{Payload=MessagePackSerializer.Serialize(original)};
        await ingress.PublishRetainedToCoreAsync(frame);
        await ((TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation.IRecoveredFuturesItiPublisher)publisher).Received(1)
            .PublishRecoveredAsync(Arg.Is<TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent>(value=>value.Id==original.Id&&value.SourceGenerationId==Generation&&value.SourceDataset==frame.Dataset),Arg.Any<CancellationToken>());
        Assert.False(registry.TryGet(frame.Dataset,out _));
        Assert.Equal(-1,registry.LastConfirmedSequence(new(frame.Dataset,frame.ValueDate,Worker,Generation,1)));
    }

    [Fact]
    public void Option_no_update_is_confirmed_only_by_the_exact_admission_and_previously_delivered_latest_value()
    {
        var registry=new DatasetWorkerAdmissionRegistry();var identity=new DatasetWorkerAdmission("GLBX.MDP3",new(2026,11,26),Worker,Generation,1);
        registry.Admit(identity);var time=new DateTimeOffset(2026,11,26,14,30,0,TimeSpan.Zero);
        Assert.False(registry.HasConfirmedOptionObservation(identity,"ES-call",2,time,time,false));
        registry.RecordOptionObservation(identity,"ES-call",2,time,time,false);
        Assert.True(registry.HasConfirmedOptionObservation(identity,"ES-call",2,time,time,false));
        Assert.True(registry.HasConfirmedOptionObservation(identity,"ES-call",3,time.AddSeconds(-1),time,false));
        Assert.False(registry.HasConfirmedOptionObservation(identity,"ES-call",3,time.AddSeconds(1),time,false));
        Assert.False(registry.HasConfirmedOptionObservation(identity,"ES-call",2,time,time,true));
        Assert.False(registry.HasConfirmedOptionObservation(identity with{GenerationId=Guid.NewGuid()},"ES-call",2,time,time,false));
    }

    [Fact]
    public async Task Archive_success_with_unadmitted_mirror_must_not_release_original_native_source()
    {
        await using var archive=new DurableRealtimeSessionArchive(Options());
        var registry=new DatasetWorkerAdmissionRegistry();var frame=Frame(DatasetPublicationKind.MarketPrice,1);
        var wire=MessagePackSerializer.Serialize(frame);
        await Assert.ThrowsAsync<IOException>(async()=>await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(archive,"isolated",false,frame,wire,
            (source,token)=>NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,source,(_,_)=>ValueTask.FromResult(false),token)));
        Assert.Equal(wire,archive.ReadCommitted().Single().Record.OriginalWire.ToArray());
        // The identical original is retried after its exact Starting admission becomes available.
        registry.Admit(new(frame.Dataset,frame.ValueDate,frame.WorkerInstanceId,frame.GenerationId,frame.ManifestRevision));
        Assert.True(await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(archive,"isolated",false,frame,wire,
            (source,token)=>NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,source,async(value,ct)=>
            {
                using var ownership=await registry.BeginPublicationAsync(new(value.Dataset,value.ValueDate,value.WorkerInstanceId,value.GenerationId,value.ManifestRevision),value.PublicationSequence,new string('a',64),ct);
                Assert.NotNull(ownership);ownership.ConfirmCapture();return true;
            },token)));
        Assert.False(await NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,frame,(_,_)=>ValueTask.FromResult(false)));
        Assert.Single(archive.ReadCommitted());
    }

    [Fact]
    public async Task Partially_mirrored_group_retry_requires_every_original_to_be_confirmed()
    {
        await using var archive=new DurableRealtimeSessionArchive(Options());var registry=new DatasetWorkerAdmissionRegistry();
        var frames=new[]{Frame(DatasetPublicationKind.Trade,1),Frame(DatasetPublicationKind.MarketPrice,2)};
        registry.Admit(new(frames[0].Dataset,frames[0].ValueDate,Worker,Generation,1));
        using(var pending=await registry.BeginPublicationAsync(new(frames[0].Dataset,frames[0].ValueDate,Worker,Generation,1),1,new string('b',64),default))pending!.ConfirmCapture();
        var wires=frames.Select(row=>(ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(row)).ToArray();
        await Assert.ThrowsAsync<IOException>(async()=>await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(archive,"isolated",false,frames,wires,
            (source,token)=>NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,source,(_,_)=>ValueTask.FromResult(false),token)));
        Assert.Equal(2,archive.ReadCommitted().Count());Assert.Equal(1,registry.LastConfirmedSequence(new(frames[0].Dataset,frames[0].ValueDate,Worker,Generation,1)));
        var other=frames[0] with{GenerationId=Guid.NewGuid()};
        await Assert.ThrowsAsync<IOException>(async()=>await NativeSessionCaptureOnlyIngress.MirrorConfirmedAsync(registry,other,(_,_)=>ValueTask.FromResult(false)));
    }

    static readonly Guid Worker = new("00000001-0000-0000-0000-000000000000"), Generation = new("00000002-0000-0000-0000-000000000000");
    static DatasetPublicationEnvelope Frame(DatasetPublicationKind kind, long sequence) => new() {
        Dataset = "GLBX.MDP3", ValueDate = new(2026,11,30), WorkerInstanceId = Worker, GenerationId = Generation,
        ManifestRevision = 1, PublicationSequence = sequence, Kind = kind, Payload = [1, 2, (byte)kind]
    };
    static RealtimeSessionArchiveOptions Options() => new() { Enabled = true,
        Directory = Path.Combine(Path.GetTempPath(), "ifm-native-session-archive", Guid.NewGuid().ToString("N")), SessionId = "isolated-test" };

    [Fact]
    public async Task All_seven_native_families_archive_exact_original_wire_before_mirror_without_a_durable_mailbox()
    {
        await using var archive = new DurableRealtimeSessionArchive(Options());
        var mirrors = new List<DatasetPublicationKind>();
        foreach (var kind in Enum.GetValues<DatasetPublicationKind>())
        {
            var frame = Frame(kind, (long)kind); MessageOrigins.Restore(frame, null);
            var wire = MessagePackSerializer.Serialize(frame);
            Assert.True(await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(archive, "isolated", (byte)kind >= 6, frame, wire,
                (original, _) => {
                    var saved = archive.ReadCommitted().Single(entry => entry.Record.Sequence == original.PublicationSequence);
                    Assert.Equal(wire, saved.Record.OriginalWire.ToArray()); Assert.Null(saved.Record.Origin);
                    Assert.Empty(saved.Record.Destinations); mirrors.Add(original.Kind); return ValueTask.FromResult(true);
                }));
        }
        Assert.Equal(7, mirrors.Count); Assert.Equal(7, archive.ReadCommitted().Count());
        Assert.Equal(0, archive.PendingRecords);
        // Only original source ownership was obtained. No actor service/broker/domain receiver exists in this test.
    }

    [Fact]
    public async Task Lost_pre_Core_forward_retries_after_cold_reopen_with_the_original_native_body_and_identity()
    {
        var options = Options(); var frame = Frame(DatasetPublicationKind.Trade, 1);
        var origin = new MessageOrigin(new DateTimeOffset(2026,11,30,1,2,3,TimeSpan.Zero), "original-native"); MessageOrigins.Restore(frame, origin);
        var wire = MessagePackSerializer.Serialize(frame);
        await using (var first = new DurableRealtimeSessionArchive(options))
        {
            await Assert.ThrowsAsync<IOException>(async () => await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(first, "isolated", false, frame, wire,
                (_, _) => ValueTask.FromException<bool>(new IOException("Original source is durable, Core mirror unavailable."))));
            Assert.Equal(wire, first.ReadCommitted().Single().Record.OriginalWire.ToArray());
        }
        await using var recovered = new DurableRealtimeSessionArchive(options);
        var calls = 0;
        Assert.False(await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(recovered, "isolated", false, frame, wire,
            (_, _) => { calls++; return ValueTask.FromResult(false); }));
        var entry = recovered.ReadCommitted().Single(); Assert.Equal(wire, entry.Record.OriginalWire.ToArray()); Assert.Equal(origin, entry.Record.Origin);
        Assert.Equal(1, calls); Assert.Equal(1, entry.Record.Sequence); Assert.Equal(Worker, entry.Record.WorkerInstanceId);
        // A false/obsolete live-mirror result is not rewritten into a successful business outcome.
    }

    [Fact]
    public async Task Archive_failure_prevents_every_mirror_and_group_retry_preserves_order_after_partial_mirror()
    {
        var frames = new[] { Frame(DatasetPublicationKind.Trade, 1), Frame(DatasetPublicationKind.MarketPrice, 2) };
        var wires = frames.Select(frame => (ReadOnlyMemory<byte>)MessagePackSerializer.Serialize(frame)).ToArray();
        var failed = Substitute.For<IRealtimeSessionRecorder>(); failed.Enabled.Returns(true);
        failed.RecordAsync(Arg.Any<RealtimeSessionArchiveRecord>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<RealtimeSessionArchiveReceipt>(new IOException("Archive refused source ownership.")));
        var mirrors = new List<long>();
        await Assert.ThrowsAsync<IOException>(async () => await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(failed, "isolated", false, frames, wires,
            (frame, _) => { mirrors.Add(frame.PublicationSequence); return ValueTask.FromResult(true); }));
        Assert.Empty(mirrors);
        var options = Options();
        await using (var first = new DurableRealtimeSessionArchive(options))
        {
            await Assert.ThrowsAsync<IOException>(async () => await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(first, "isolated", false, frames, wires,
                (frame, _) => { Assert.Equal(2, first.ReadCommitted().Count()); mirrors.Add(frame.PublicationSequence);
                    return frame.PublicationSequence == 2 ? ValueTask.FromException<bool>(new IOException("Second Core branch failed.")) : ValueTask.FromResult(true); }));
        }
        await using var recovered = new DurableRealtimeSessionArchive(options);
        await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(recovered, "isolated", false, frames, wires,
            (frame, _) => { mirrors.Add(frame.PublicationSequence); return ValueTask.FromResult(true); });
        Assert.Equal(new long[] {1,2,1,2}, mirrors); Assert.Equal(2, recovered.ReadCommitted().Count());
    }

    [Fact]
    public async Task Mixed_source_group_or_changed_original_wire_is_rejected_before_forwarding()
    {
        await using var archive = new DurableRealtimeSessionArchive(Options());
        var frame = Frame(DatasetPublicationKind.Trade, 1); var wire = MessagePackSerializer.Serialize(frame); var calls = 0;
        ValueTask<bool> Mirror(DatasetPublicationEnvelope _, CancellationToken __) { calls++; return ValueTask.FromResult(true); }
        await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(archive, "isolated", false, frame, wire, Mirror);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeSessionCaptureOnlyIngress.RecordThenMirrorAsync(archive, "isolated", false,
            frame, wire.Concat(new byte[] { 0 }).ToArray(), Mirror));
        var mixed = frame with { PublicationSequence = 2, GenerationId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeSessionCaptureOnlyIngress.RecordBatchThenMirrorAsync(archive, "isolated", false,
            [frame, mixed], [wire, MessagePackSerializer.Serialize(mixed)], Mirror));
        Assert.Equal(1, calls);
    }
}
