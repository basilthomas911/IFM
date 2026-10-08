using System.Buffers.Binary;
using TomasAI.IFM.Application.MarketData.Pricing;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public enum DatasetPublicationKind : byte
{
    Trade = 1,
    Quote = 2,
    MarketPrice = 3,
    SessionStatistics = 4,
    TradeReplayBatch = 5,
    OptionTradeEvidence = 6,
    OptionQuoteObservation = 7
}

[MessagePackObject]
public sealed record DatasetPublicationEnvelope
{
    [Key(0)] public required string Dataset { get; init; }
    [Key(1)] public required DateOnly ValueDate { get; init; }
    [Key(2)] public required Guid WorkerInstanceId { get; init; }
    [Key(3)] public required Guid GenerationId { get; init; }
    [Key(4)] public required long ManifestRevision { get; init; }
    [Key(5)] public required long PublicationSequence { get; init; }
    [Key(6)] public required DatasetPublicationKind Kind { get; init; }
    [Key(7)] public required byte[] Payload { get; init; }
}

public static class DatasetPublicationFrameCodec
{
    public const int MaximumFrameBytes = 1024 * 1024;

    public static async ValueTask WriteAsync(Stream stream, DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken)
    {
        Validate(envelope);
        var payload = MessagePackSerializer.Serialize(envelope);
        if (payload.Length > MaximumFrameBytes)
            throw new InvalidDataException("Dataset publication frame is too large.");
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<DatasetPublicationEnvelope> ReadAsync(Stream stream,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 2 or > MaximumFrameBytes)
            throw new InvalidDataException($"Dataset publication frame length {length} is invalid.");
        var payload = GC.AllocateUninitializedArray<byte>(length);
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var envelope = MessagePackSerializer.Deserialize<DatasetPublicationEnvelope>(payload);
        Validate(envelope);
        return envelope;
    }

    static void Validate(DatasetPublicationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(envelope.Dataset) || envelope.Dataset.Length > 64
            || envelope.ValueDate == default || envelope.WorkerInstanceId == Guid.Empty
            || envelope.GenerationId == Guid.Empty || envelope.ManifestRevision < 1
            || envelope.PublicationSequence < 1 || !Enum.IsDefined(envelope.Kind)
            || envelope.Payload is null || envelope.Payload.Length == 0
            || envelope.Payload.Length > MaximumFrameBytes)
            throw new InvalidDataException("Dataset publication identity or bounds are invalid.");
    }
}

/// <summary>Rejects stale worker output before translating it to the existing realtime publisher.</summary>
public sealed class DatasetPublicationIngress(
    DatasetWorkerAdmissionRegistry admissions,
    ITickAggregationEventPublisher publisher,
    IMarketDataOperationsRecorder recorder,
    DatasetWorkerCurrentValues? currentValues = null, Pricing.IOptionTradeEvidenceWriter? optionTrades = null)
{
    readonly object holdingGate = new();
    readonly Dictionary<string, CandidateHoldingState> candidateHolding = new(StringComparer.Ordinal);

    /// <summary>
    /// Authorizes only the locally qualified candidate generation to enter a finite holding
    /// buffer. This does not admit it to the publisher, current-values mirror, or actor routes.
    /// The previous generation must already have been fenced by the hard-recovery owner.
    /// </summary>
    public void StartCandidateHolding(DatasetWorkerAdmission identity,
        DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope> buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (string.IsNullOrWhiteSpace(identity.Dataset) || identity.ValueDate == default
            || identity.WorkerInstanceId == Guid.Empty || identity.GenerationId == Guid.Empty
            || identity.ManifestRevision < 1)
            throw new ArgumentException("Candidate holding identity is invalid.", nameof(identity));
        lock (holdingGate)
        {
            if (candidateHolding.ContainsKey(identity.Dataset))
                throw new InvalidOperationException("A candidate generation is already being held for this dataset.");
            if (candidateHolding.Count >= DatasetDesiredSubscriptionRegistry.MaximumDatasets)
                throw new InvalidOperationException("Candidate holding dataset capacity is exhausted.");
            if (admissions.TryGet(identity.Dataset, out _))
                throw new InvalidOperationException("The previous generation must be fenced before candidate holding.");
            candidateHolding.Add(identity.Dataset, new CandidateHoldingState(identity, buffer));
        }
    }

    /// <summary>Stops accepting candidate records; already held records remain owned by the caller's buffer.</summary>
    public void StopCandidateHolding(DatasetWorkerAdmission identity)
    {
        lock (holdingGate)
        {
            if (!candidateHolding.TryGetValue(identity.Dataset, out var holding) || holding.Identity != identity)
                throw new InvalidOperationException("The active candidate holding identity differs.");
            if (holding.ProbeInFlight)
                throw new InvalidOperationException("The candidate probe has not finished publication.");
            candidateHolding.Remove(identity.Dataset);
        }
    }

    /// <summary>Releases all qualified datasets from holding under one ingress lock.</summary>
    public void PromoteCandidateHoldings(IReadOnlyCollection<DatasetWorkerAdmission> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);
        if (identities.Count == 0 || identities.Count > DatasetDesiredSubscriptionRegistry.MaximumDatasets
            || identities.Select(identity => identity.Dataset).Distinct(StringComparer.Ordinal).Count() != identities.Count)
            throw new ArgumentException("Promotion requires a nonempty, unique dataset set.", nameof(identities));
        lock (holdingGate)
        {
            foreach (var identity in identities)
            {
                if (!candidateHolding.TryGetValue(identity.Dataset, out var holding)
                    || holding.Identity != identity || holding.ProbeInFlight
                    || !admissions.TryGet(identity.Dataset, out var admitted) || admitted != identity)
                    throw new InvalidOperationException("Every candidate must be admitted and idle before promotion.");
            }
            foreach (var identity in identities) candidateHolding.Remove(identity.Dataset);
        }
    }

    /// <summary>
    /// Publishes one held candidate trade or quote solely for the scoped tick-storage proof.
    /// The caller must arm and later drain the exact actor probe admission; this method does
    /// not admit normal ingress, current values, market prices, analytics, or option evidence.
    /// </summary>
    public async Task<bool> PublishHeldCandidateTickProbeAsync(DatasetWorkerAdmission identity,
        CancellationToken cancellationToken = default)
    {
        DatasetPublicationEnvelope envelope;
        CandidateHoldingState holding;
        lock (holdingGate)
        {
            if (!candidateHolding.TryGetValue(identity.Dataset, out holding!) || holding.Identity != identity
                || holding.ProbeInFlight)
                throw new InvalidOperationException("The candidate probe identity or state differs.");
            if (!holding.Buffer.TryTakeWhere(static candidate =>
                    candidate.Kind is DatasetPublicationKind.Trade or DatasetPublicationKind.Quote,
                    out envelope!))
                return false;
            if (envelope.Dataset != identity.Dataset || envelope.ValueDate != identity.ValueDate
                || envelope.WorkerInstanceId != identity.WorkerInstanceId
                || envelope.GenerationId != identity.GenerationId
                || envelope.ManifestRevision != identity.ManifestRevision)
                throw new InvalidDataException("Held candidate tick identity differs from the probe.");
            holding.ProbeInFlight = true;
        }
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (envelope.Kind == DatasetPublicationKind.Trade)
                await publisher.PublishAsync(
                    MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload) with
                    { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId },
                    cancellationToken).ConfigureAwait(false);
            else
            {
                var quote = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload) with
                { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId };
                await publisher.PublishAsync(quote,
                    new DeserializedQuoteLease(quote.QuoteData.Buffer, quote.QuoteCount),
                    cancellationToken).ConfigureAwait(false);
            }
            Record(MarketDataOperationOutcome.Published, envelope);
            return true;
        }
        finally
        {
            lock (holdingGate) holding.ProbeInFlight = false;
        }
    }

    public async ValueTask<bool> AcceptAsync(DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = new DatasetWorkerAdmission(envelope.Dataset, envelope.ValueDate,
            envelope.WorkerInstanceId, envelope.GenerationId, envelope.ManifestRevision);
        Record(MarketDataOperationOutcome.Received, envelope);
        lock (holdingGate)
        {
            if (candidateHolding.TryGetValue(identity.Dataset, out var holding))
            {
                if (holding.Identity != identity || envelope.PublicationSequence <= holding.LastSequence
                    || envelope.PublicationSequence < 1 || envelope.Payload is null
                    || envelope.Payload.Length is < 1 or > DatasetPublicationFrameCodec.MaximumFrameBytes)
                {
                    Record(MarketDataOperationOutcome.Failed, envelope);
                    return false;
                }
                holding.LastSequence = envelope.PublicationSequence;
                var held = holding.Buffer.TryHold(envelope);
                Record(held ? MarketDataOperationOutcome.Enqueued : MarketDataOperationOutcome.Failed, envelope);
                return held;
            }
        }
        if (!admissions.TryAccept(identity, envelope.PublicationSequence, out var generationCancellation))
        {
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }
        // The mirror also verifies identity under its own reset lock. A publication that passed
        // admission just before Close cannot repopulate cleared dataset values afterward.
        if (currentValues is not null && !currentValues.AcceptPublication(envelope))
        {
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            generationCancellation.ThrowIfCancellationRequested();
            // The publisher enqueues this token and returns before transmission. A short-lived
            // linked CTS would disconnect reset cancellation at that return boundary. Ownership
            // therefore transfers with the lasting generation token, not the caller's read token.
            switch (envelope.Kind)
            {
                case DatasetPublicationKind.OptionTradeEvidence:
                    var evidence = MessagePackSerializer.Deserialize<Pricing.OptionTradeEvidence>(envelope.Payload);
                    evidence.Validate();
                    if (evidence.Source.Dataset != envelope.Dataset || evidence.Source.ValueDate != envelope.ValueDate
                        || evidence.Source.GenerationId != envelope.GenerationId)
                        throw new InvalidDataException("Option trade source and worker envelope disagree.");
                    if (optionTrades is null) throw new InvalidOperationException("Durable option trade writer is unavailable.");
                    await optionTrades.WriteAsync(evidence, generationCancellation).ConfigureAwait(false);
                    generationCancellation.ThrowIfCancellationRequested();
                    await publisher.PublishAsync(evidence.ToRealtimeEvent(), generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.OptionQuoteObservation:
                case DatasetPublicationKind.Trade:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId },
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.Quote:
                    var quote = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload) with
                    { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId };
                    await publisher.PublishAsync(quote,
                        new DeserializedQuoteLease(quote.QuoteData.Buffer, quote.QuoteCount),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.MarketPrice:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId },
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.TradeReplayBatch:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(envelope.Payload) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId },
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.SessionStatistics:
                    await publisher.PublishAsync(
                        MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId },
                        generationCancellation).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidDataException("Unknown dataset publication kind.");
            }
        }
        catch (OperationCanceledException) when (generationCancellation.IsCancellationRequested)
        {
            // An expected reset must not fault the shared publication reader/data plane.
            Record(MarketDataOperationOutcome.Failed, envelope);
            return false;
        }
        Record(MarketDataOperationOutcome.Published, envelope);
        return true;
    }

    void Record(MarketDataOperationOutcome outcome, DatasetPublicationEnvelope envelope)
    {
        try
        {
            recorder.Record(new MarketDataOperationMeasurement(
                MarketDataOperationStage.DatabentoGenerationIngress, outcome,
                MarketOutlookUpdateKind.FeedHealth, Guid.Empty, DateTime.UtcNow));
        }
        catch
        {
            // Operational telemetry is never allowed to interrupt realtime ingress.
        }
    }

    sealed class DeserializedQuoteLease(FuturesTickQuoteData[] buffer, ushort count)
        : ITickQuoteBufferLease
    {
        public FuturesTickQuoteData[] Buffer { get; } = buffer;
        public ushort Count { get; private set; } = count;
        public void SetCount(ushort value)
        {
            if (value > Buffer.Length) throw new ArgumentOutOfRangeException(nameof(value));
            Count = value;
        }
        public void Dispose() { }
    }

    sealed class CandidateHoldingState(DatasetWorkerAdmission identity,
        DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope> buffer)
    {
        public DatasetWorkerAdmission Identity { get; } = identity;
        public DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope> Buffer { get; } = buffer;
        public long LastSequence;
        public bool ProbeInFlight;
    }
}
