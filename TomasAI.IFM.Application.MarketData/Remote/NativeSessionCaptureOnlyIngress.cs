using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Archive ownership before unchanged legacy forwarding. It never reports a domain-handler acknowledgement.</summary>
public static class NativeSessionCaptureOnlyIngress
{
    /// <summary>Archive ownership alone must not acknowledge an unadmitted or failed Core mirror.</summary>
    public static async ValueTask<bool> MirrorConfirmedAsync(DatasetWorkerAdmissionRegistry registry,
        DatasetPublicationEnvelope frame, Func<DatasetPublicationEnvelope,CancellationToken,ValueTask<bool>> mirror,
        CancellationToken token=default)
    {
        if(await mirror(frame,token).ConfigureAwait(false))return true;
        var identity=new DatasetWorkerAdmission(frame.Dataset,frame.ValueDate,frame.WorkerInstanceId,frame.GenerationId,frame.ManifestRevision);
        if(frame.Kind==DatasetPublicationKind.OptionQuoteObservation)
        {
            var observation=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(frame.Payload,cancellationToken:token)
                .OptionMarketPriceObservation?.OptionTickData??throw new InvalidDataException("Missing original option observation.");
            if(registry.HasConfirmedOptionObservation(identity,observation.ContractId,frame.PublicationSequence,
                observation.ObservedAtUtc,observation.CalculatedAtUtc,observation.IsPricingUpdate))return false;
        }
        else if(registry.LastConfirmedSequence(identity)>=frame.PublicationSequence)return false;
        throw new IOException("Original source is archived but its live mirror is not confirmed; native ownership must remain retained.");
    }

    /// <summary>Freezes one original native source and mirrors it only after durable archive acceptance.</summary>
    public static async ValueTask<bool> RecordThenMirrorAsync(IRealtimeSessionRecorder recorder, string scope, bool options,
        DatasetPublicationEnvelope frame, ReadOnlyMemory<byte> originalWire,
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask<bool>> mirror, CancellationToken token = default)
    {
        await RecordAsync(recorder, scope, options, frame, originalWire, token).ConfigureAwait(false);
        return await mirror(frame, token).ConfigureAwait(false);
    }

    /// <summary>All members must be archived before any mirror call; partial archive failure retains the original upstream group.</summary>
    public static async ValueTask<bool[]> RecordBatchThenMirrorAsync(IRealtimeSessionRecorder recorder, string scope, bool options,
        IReadOnlyList<DatasetPublicationEnvelope> frames, IReadOnlyList<ReadOnlyMemory<byte>> originalWires,
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask<bool>> mirror, CancellationToken token = default)
    {
        if (frames.Count is < 1 or > 64 || frames.Count != originalWires.Count) throw new InvalidDataException("Invalid capture-only source group.");
        var first = frames[0]; long previous = 0;
        foreach (var frame in frames)
        {
            Validate(scope, frame);
            if (frame.Dataset != first.Dataset || frame.WorkerInstanceId != first.WorkerInstanceId || frame.GenerationId != first.GenerationId
                || frame.ValueDate != first.ValueDate || frame.ManifestRevision != first.ManifestRevision || frame.PublicationSequence <= previous)
                throw new InvalidDataException("Capture-only group mixes original identity or order.");
            previous = frame.PublicationSequence;
        }
        if (originalWires.Sum(value => (long)value.Length) > 576 * 1024 || frames.Sum(value => (long)value.Payload.Length) > 576 * 1024)
            throw new InvalidDataException("Capture-only group aggregate bytes exceed bounds.");
        // The upstream protocol already enforces the aggregate expanded byte bound. Each record fsync participates in the bounded archive group commit.
        await Task.WhenAll(frames.Select((frame, index) => RecordAsync(recorder, scope, options, frame, originalWires[index], token).AsTask())).ConfigureAwait(false);
        var live = new bool[frames.Count];
        for (var index = 0; index < frames.Count; index++) live[index] = await mirror(frames[index], token).ConfigureAwait(false);
        return live;
    }

    static async ValueTask RecordAsync(IRealtimeSessionRecorder recorder, string scope, bool options,
        DatasetPublicationEnvelope frame, ReadOnlyMemory<byte> originalWire, CancellationToken token)
    {
        if (!recorder.Enabled) throw new InvalidOperationException("Capture-only source archive is disabled.");
        Validate(scope, frame);
        if (originalWire.Length is < 1 or > 2 * 1024 * 1024) throw new InvalidDataException("Capture-only source wire exceeds bounds.");
        var role = options ? "Options" : "Futures";
        var key = $"native/v1|{scope}|{role}|{frame.Dataset}|{frame.WorkerInstanceId:N}|{frame.GenerationId:N}|{frame.ValueDate:yyyy-MM-dd}|{frame.ManifestRevision}|{frame.PublicationSequence}";
        var receipt = await recorder.RecordAsync(new RealtimeSessionArchiveRecord {
            Kind = "NativeSource/" + role + "/" + frame.Kind, LogicalSourceKey = key,
            Subject = scope + ".source-capture." + role, OriginalWire = originalWire, Origin = MessageOrigins.Get(frame), Dataset = frame.Dataset,
            WorkerInstanceId = frame.WorkerInstanceId, GenerationId = frame.GenerationId, Sequence = frame.PublicationSequence
        }, token).ConfigureAwait(false);
        if (!receipt.ArchiveOwnershipOnly || !string.Equals(receipt.OriginalWireSha256,
            Convert.ToHexString(SHA256.HashData(originalWire.Span)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Capture-only archive receipt does not prove the original native wire.");
    }
    static void Validate(string scope, DatasetPublicationEnvelope frame)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > 256 || string.IsNullOrWhiteSpace(frame.Dataset)
            || frame.WorkerInstanceId == Guid.Empty || frame.GenerationId == Guid.Empty || frame.PublicationSequence < 1
            || frame.ManifestRevision < 1 || frame.ValueDate == default || frame.Payload is not { Length: > 0 and <= 512 * 1024 })
            throw new InvalidDataException("Invalid immutable native source authority or payload.");
    }
}
