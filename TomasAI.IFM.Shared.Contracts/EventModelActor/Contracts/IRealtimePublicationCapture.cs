namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Owns immutable required publications before the source may advance. Acceptance is not handler completion.</summary>
public interface IRealtimePublicationCapture
{
    bool Enabled { get; }
    ValueTask<RealtimeCaptureReceipt> CaptureAsync(RealtimeSourcePublication source, CancellationToken cancellationToken = default);
    async ValueTask<IReadOnlyList<RealtimeCaptureReceipt>> CaptureBatchAsync(IReadOnlyList<RealtimeSourcePublication> sources, CancellationToken cancellationToken = default)
    {
        if (sources.Count is < 1 or > 64) throw new InvalidDataException("Realtime capture batch size is outside bounds.");
        var results = new List<RealtimeCaptureReceipt>(sources.Count);
        foreach (var source in sources) results.Add(await CaptureAsync(source, cancellationToken).ConfigureAwait(false));
        return results;
    }
}

/// <summary>The original wire bytes and provenance. The caller retains its buffer until CaptureAsync completes.</summary>
public sealed record RealtimeSourcePublication(ActorSubject Subject, Guid SourceEventId, ReadOnlyMemory<byte> Payload,
    MessageOrigin? Origin = null, string? Dataset = null, Guid SourceGenerationId = default, string? TraceParent = null, string? TraceState = null);

/// <summary>Evidence that a broker or persistent outbox owns every frozen required destination.</summary>
public sealed record RealtimeCaptureReceipt(Guid SourceEventId, string SourceSha256, string ManifestSha256,
    int DestinationCount, bool AlreadyCaptured);

/// <summary>A fetched broker delivery consumes bounded in-flight capacity, not the old shared queued-message budget.</summary>
public interface IRealtimeBrokerDelivery : IActorDeliveryCompletion
{
    /// <summary>Generation fencing requires an explicit durable disposition, not a successful business ACK.</summary>
    ValueTask CompleteFencedAsync();
}

/// <summary>Generation replacement waits for all previously captured required handlers, not just publication ACKs.</summary>
public interface IRealtimeGenerationDrain
{
    ValueTask WaitForDrainAsync(string dataset, Guid generation, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<RealtimePendingGeneration>> GetPendingGenerationsAsync(CancellationToken cancellationToken = default);
}

public sealed record RealtimePendingGeneration(string Dataset, Guid Generation, long FirstCaptureOrdinal, int SourceCount);
