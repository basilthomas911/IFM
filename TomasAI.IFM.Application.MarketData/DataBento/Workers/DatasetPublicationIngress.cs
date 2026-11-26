using System.Buffers.Binary;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.MarketData.Pricing;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Rejects stale worker output before translating it to the existing realtime publisher.</summary>
public sealed class DatasetPublicationIngress(
    DatasetWorkerAdmissionRegistry admissions,
    ITickAggregationEventPublisher publisher,
    IMarketDataOperationsRecorder recorder,
    DatasetWorkerCurrentValues? currentValues = null, Pricing.IOptionTradeEvidenceWriter? optionTrades = null, bool evidencePersistedByHost = false,
    Storage.IRawFuturesEodSink? rawEod = null, bool requireDurableCapture = false)
{
    /// <summary>Creates an independent admission boundary for option-only worker output.</summary>
    public DatasetPublicationIngress ForOptions(DatasetWorkerAdmissionRegistry optionAdmissions)
        => new(optionAdmissions, publisher, recorder, optionTrades: optionTrades, evidencePersistedByHost: evidencePersistedByHost,requireDurableCapture:requireDurableCapture);

    readonly System.Threading.Lock holdingGate = new();
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "PublishAsync takes ownership of the deserialized lease, including failure cleanup; disposing at the call site would invalidate asynchronous consumption.")]
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
            if (!string.Equals(envelope.Dataset,identity.Dataset,StringComparison.Ordinal) || envelope.ValueDate != identity.ValueDate
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
                    TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,
                        MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId }),
                    cancellationToken).ConfigureAwait(false);
            else
            {
                var quote = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId };
                TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, quote);
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

    readonly SemaphoreSlim optionDelivery = new(1, 1);
    public async ValueTask<bool> AcceptAsync(DatasetPublicationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Kind != DatasetPublicationKind.OptionQuoteObservation)
            return await AcceptCoreAsync(envelope, cancellationToken).ConfigureAwait(false);
        await optionDelivery.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await AcceptCoreAsync(envelope, cancellationToken).ConfigureAwait(false); }
        finally { optionDelivery.Release(); }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "PublishAsync takes ownership of the deserialized lease, including failure cleanup; disposing at the call site would invalidate asynchronous consumption.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep native resource ownership and recovery failure ordering together during this storage compatibility refactor.")]
    async ValueTask<bool> AcceptCoreAsync(DatasetPublicationEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
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
                var held = holding.Buffer.TryHold(envelope);
                if(held)holding.LastSequence = envelope.PublicationSequence;
                Record(held ? MarketDataOperationOutcome.Enqueued : MarketDataOperationOutcome.Failed, envelope);
                return held;
            }
        }
        CancellationToken generationCancellation;
        var accepted = false;
        using var pending = envelope.Kind == DatasetPublicationKind.OptionQuoteObservation ? null
            : await admissions.BeginPublicationAsync(identity,envelope.PublicationSequence,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(envelope))),cancellationToken).ConfigureAwait(false);
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel? optionObservation = null;
        if (envelope.Kind == DatasetPublicationKind.OptionQuoteObservation)
        {
            if (!admissions.TryGet(identity.Dataset, out var admitted) || admitted != identity)
            { Record(MarketDataOperationOutcome.Failed, envelope); return false; }
            var observation = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload,
                cancellationToken: cancellationToken).OptionMarketPriceObservation?.OptionTickData
                ?? throw new InvalidDataException("Missing option observation.");
            if (observation.SourceGeneration != envelope.GenerationId || observation.ValueDate != envelope.ValueDate)
                throw new InvalidDataException("Option observation generation/value date differs from envelope.");
            optionObservation = observation;
            accepted = admissions.TryAcceptOptionObservation(identity, observation.ContractId, envelope.PublicationSequence,
                observation.ObservedAtUtc, observation.CalculatedAtUtc, observation.IsPricingUpdate, out generationCancellation);
        }
        else {accepted=pending is not null;generationCancellation=pending?.GenerationCancellation??default;}
        if (!accepted)
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
            if(requireDurableCapture)await PublishRequiredAsync(envelope,generationCancellation).ConfigureAwait(false);
            else switch (envelope.Kind)
            {
                case DatasetPublicationKind.OptionTradeEvidence:
                    var evidence = MessagePackSerializer.Deserialize<Pricing.OptionTradeEvidence>(envelope.Payload,cancellationToken:cancellationToken);
                    evidence.Validate();
                    if (!string.Equals(evidence.Source.Dataset,envelope.Dataset,StringComparison.Ordinal) || evidence.Source.ValueDate != envelope.ValueDate
                        || evidence.Source.GenerationId != envelope.GenerationId)
                        throw new InvalidDataException("Option trade source and worker envelope disagree.");
                    if (optionTrades is not null) await optionTrades.WriteAsync(evidence, generationCancellation).ConfigureAwait(false);
                    else if (!evidencePersistedByHost) throw new InvalidOperationException("Durable option trade writer is unavailable.");
                    generationCancellation.ThrowIfCancellationRequested();
                    await publisher.PublishAsync(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, evidence.ToRealtimeEvent()), generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.OptionQuoteObservation:
                case DatasetPublicationKind.Trade:
                    await publisher.PublishAsync(
                        TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId }),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.Quote:
                    if (!requireDurableCapture&&AppContext.TryGetSwitch("IFM.RawFuturesDurableStorage", out var rawStorage) && rawStorage) break;
                    var quote = MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                    { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId };
                    TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, quote);
                    await publisher.PublishAsync(quote,
                        new DeserializedQuoteLease(quote.QuoteData.Buffer, quote.QuoteCount),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.MarketPrice:
                    var price = TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,
                        MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload, cancellationToken: cancellationToken) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId });
                    rawEod?.Observe(price);
                    await publisher.PublishAsync(
                        price,
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.TradeReplayBatch:
                    await publisher.PublishAsync(
                        TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId }),
                        generationCancellation).ConfigureAwait(false);
                    break;
                case DatasetPublicationKind.SessionStatistics:
                    if (rawEod is not null)
                    {
                        rawEod.Observe(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,
                            MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload, cancellationToken: cancellationToken) with
                            { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId }));
                        if(!requireDurableCapture)break;
                    }
                    await publisher.PublishAsync(
                        TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope, MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload,cancellationToken:cancellationToken) with
                        { SourceDataset = envelope.Dataset, SourceGenerationId = envelope.GenerationId }),
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
        pending?.ConfirmCapture();
        // Read-view progress is advisory and cannot veto a confirmed required capture.
        _=currentValues?.AcceptPublication(envelope);
        if (optionObservation is { } delivered)
            admissions.RecordOptionObservation(identity, delivered.ContractId, envelope.PublicationSequence, delivered.ObservedAtUtc, delivered.CalculatedAtUtc, delivered.IsPricingUpdate);
        Record(MarketDataOperationOutcome.Published, envelope);
        return true;
    }

    /// <summary>Finite original-source group, preserving capture and ITI release order. Replayed confirmed prefixes are separated from fresh suffixes.</summary>
    public async ValueTask<bool[]> CaptureSourceBatchAsync(IReadOnlyList<DatasetPublicationEnvelope> frames,CancellationToken token=default)
    {
        if(!requireDurableCapture||publisher is not IDurableTickPublicationBatch batchPublisher)throw new InvalidOperationException("Confirmed source grouping is unavailable.");
        if(frames.Count is <1 or >64)throw new InvalidDataException("Source group size exceeds bounds.");
        var first=frames[0];var identity=new DatasetWorkerAdmission(first.Dataset,first.ValueDate,first.WorkerInstanceId,first.GenerationId,first.ManifestRevision);
        long previous=0;
        foreach(var frame in frames)
        {
            if(frame.Dataset!=identity.Dataset||frame.ValueDate!=identity.ValueDate||frame.WorkerInstanceId!=identity.WorkerInstanceId||frame.GenerationId!=identity.GenerationId||frame.ManifestRevision!=identity.ManifestRevision||frame.PublicationSequence<=previous||frame.Payload.Length is <1 or >DatasetPublicationFrameCodec.MaximumFrameBytes)
                throw new InvalidDataException("Source group mixes identity, order or invalid payload bounds.");
            previous=frame.PublicationSequence;
        }
        var live=new bool[frames.Count];var last=admissions.LastConfirmedSequence(identity);
        var split=last<0?frames.Count:frames.TakeWhile(frame=>frame.PublicationSequence<=last).Count();
        if(split>0)await PublishGroupRangeAsync(0,split,recovered:true).ConfigureAwait(false);
        if(split<frames.Count)
        {
            var start=frames[split];
            using var pending=await admissions.BeginPublicationAsync(identity,start.PublicationSequence,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePackSerializer.Serialize(start))),token).ConfigureAwait(false);
            if(pending is null){await PublishGroupRangeAsync(split,frames.Count,recovered:true).ConfigureAwait(false);return live;}
            await PublishGroupRangeAsync(split,frames.Count,recovered:false).ConfigureAwait(false);
            pending.ConfirmCaptureThrough(frames[^1].PublicationSequence);
            for(var index=split;index<frames.Count;index++)
            {
                live[index]=true;_=currentValues?.AcceptPublication(frames[index]);Record(MarketDataOperationOutcome.Published,frames[index]);
                if(frames[index].Kind==DatasetPublicationKind.OptionQuoteObservation)
                {
                    var observation=MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(frames[index].Payload).OptionMarketPriceObservation!.OptionTickData;
                    admissions.RecordOptionObservation(identity,observation.ContractId,frames[index].PublicationSequence,observation.ObservedAtUtc,observation.CalculatedAtUtc,observation.IsPricingUpdate);
                }
            }
        }
        return live;

        async ValueTask PublishGroupRangeAsync(int begin,int end,bool recovered)
        {
            var events=new List<IEvent>(64);
            async ValueTask FlushAsync(){if(events.Count==0)return;await batchPublisher.PublishBatchAsync(events.ToArray(),recovered,token).ConfigureAwait(false);events.Clear();}
            for(var index=begin;index<end;index++)
            {
                var frame=frames[index];var value=await DecodeGroupedSourceAsync(frame,token).ConfigureAwait(false);
                if(value is FuturesTradeReplayBatchRealtimeEvent||value is FuturesMarketPriceUpdatedRealtimeEvent{VwapCheckpoint:not null})
                {await FlushAsync().ConfigureAwait(false);await PublishRequiredAsync(frame,token,recovered).ConfigureAwait(false);}
                else events.Add(value);
            }
            await FlushAsync().ConfigureAwait(false);
        }
    }
    async ValueTask<IEvent> DecodeGroupedSourceAsync(DatasetPublicationEnvelope frame,CancellationToken token)
    {
        IEvent value;
        switch(frame.Kind)
        {
            case DatasetPublicationKind.OptionTradeEvidence:
                var evidence=MessagePackSerializer.Deserialize<Pricing.OptionTradeEvidence>(frame.Payload,cancellationToken:token);evidence.Validate();
                if(evidence.Source.Dataset!=frame.Dataset||evidence.Source.ValueDate!=frame.ValueDate||evidence.Source.GenerationId!=frame.GenerationId)throw new InvalidDataException("Option evidence differs from grouped source.");
                if(optionTrades is not null)await optionTrades.WriteAsync(evidence,token).ConfigureAwait(false);else if(!evidencePersistedByHost)throw new InvalidOperationException("Original option evidence is not durably owned.");
                value=evidence.ToRealtimeEvent();break;
            case DatasetPublicationKind.OptionQuoteObservation:
            case DatasetPublicationKind.Trade:
                var trade=MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(frame.Payload,cancellationToken:token);
                if(frame.Kind==DatasetPublicationKind.OptionQuoteObservation&&(trade.OptionMarketPriceObservation?.OptionTickData is not{} option||option.SourceGeneration!=frame.GenerationId||option.ValueDate!=frame.ValueDate))throw new InvalidDataException("Option observation differs from grouped source.");
                value=trade with{SourceDataset=frame.Dataset,SourceGenerationId=frame.GenerationId};break;
            case DatasetPublicationKind.Quote:value=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(frame.Payload,cancellationToken:token) with{SourceDataset=frame.Dataset,SourceGenerationId=frame.GenerationId};break;
            case DatasetPublicationKind.MarketPrice:value=MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(frame.Payload,cancellationToken:token) with{SourceDataset=frame.Dataset,SourceGenerationId=frame.GenerationId};break;
            case DatasetPublicationKind.TradeReplayBatch:value=MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(frame.Payload,cancellationToken:token) with{SourceDataset=frame.Dataset,SourceGenerationId=frame.GenerationId};break;
            case DatasetPublicationKind.SessionStatistics:value=MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(frame.Payload,cancellationToken:token) with{SourceDataset=frame.Dataset,SourceGenerationId=frame.GenerationId};break;
            default:throw new InvalidDataException("Unsupported grouped source kind.");
        }
        return TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(frame,(object)value) as IEvent??throw new InvalidDataException("Grouped source is not an event.");
    }

    /// <summary>Captures original source bytes, advancing current admission only after ownership succeeds.
    /// Retired source capture never promotes an obsolete generation into the live read view.</summary>
    public async ValueTask<bool> CaptureSourceAsync(DatasetPublicationEnvelope envelope,CancellationToken token=default)
    {
        var identity=new DatasetWorkerAdmission(envelope.Dataset,envelope.ValueDate,envelope.WorkerInstanceId,envelope.GenerationId,envelope.ManifestRevision);
        if(admissions.TryGet(envelope.Dataset,out var active)&&active==identity)
        {
            if(await AcceptAsync(envelope,token).ConfigureAwait(false))return true;
            // An ACK may have been lost after commit. Capture and ordered input ownership are idempotent.
            await CaptureRecoveredAsync(envelope,token).ConfigureAwait(false);return false;
        }
        await CaptureRecoveredAsync(envelope,token).ConfigureAwait(false);return false;
    }

    /// <summary>Replays a source-owned immutable envelope into confirmed capture, without promoting retired live admission or read views.</summary>
    public async ValueTask CaptureRecoveredAsync(DatasetPublicationEnvelope envelope,CancellationToken token=default)
    {
        if(!requireDurableCapture)throw new InvalidOperationException("Required source recovery needs confirmed durable capture.");
        if(envelope.Payload.Length is <1 or >DatasetPublicationFrameCodec.MaximumFrameBytes||envelope.WorkerInstanceId==Guid.Empty||envelope.GenerationId==Guid.Empty||envelope.PublicationSequence<1||envelope.ManifestRevision<1)
            throw new InvalidDataException("Invalid retained source publication.");
        await PublishRequiredAsync(envelope,token,recovered:true).ConfigureAwait(false);
    }

    /// <summary>Mirrors an archived retired source through Core, preserving original source identity.
    /// The caller owns its immutable archive and recovery-only actor admission; ordered ITI requires positive original coverage.</summary>
    public async ValueTask PublishRetainedToCoreAsync(DatasetPublicationEnvelope envelope,CancellationToken token=default)
    {
        if(requireDurableCapture)throw new InvalidOperationException("Core recovery cannot replace confirmed durable source capture.");
        if(envelope.Payload.Length is <1 or >DatasetPublicationFrameCodec.MaximumFrameBytes||envelope.WorkerInstanceId==Guid.Empty
            ||envelope.GenerationId==Guid.Empty||envelope.PublicationSequence<1||envelope.ManifestRevision<1)
            throw new InvalidDataException("Invalid original retained Core source.");
        await PublishRequiredAsync(envelope,token,recovered:true).ConfigureAwait(false);
    }
    async ValueTask PublishRequiredAsync(DatasetPublicationEnvelope envelope,CancellationToken token,bool recovered=false)
    {
        switch(envelope.Kind)
        {
            case DatasetPublicationKind.OptionTradeEvidence:
                var evidence=MessagePackSerializer.Deserialize<Pricing.OptionTradeEvidence>(envelope.Payload,cancellationToken:token);evidence.Validate();
                if(evidence.Source.Dataset!=envelope.Dataset||evidence.Source.ValueDate!=envelope.ValueDate||evidence.Source.GenerationId!=envelope.GenerationId)
                    throw new InvalidDataException("Retained option evidence source differs.");
                if(optionTrades is not null)await optionTrades.WriteAsync(evidence,token).ConfigureAwait(false);
                else if(!evidencePersistedByHost)throw new InvalidOperationException("Durable option evidence is unavailable.");
                await publisher.PublishAsync(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,evidence.ToRealtimeEvent()),token).ConfigureAwait(false);break;
            case DatasetPublicationKind.OptionQuoteObservation:
            case DatasetPublicationKind.Trade:
                await publisher.PublishAsync(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(envelope.Payload,cancellationToken:token) with
                    {SourceDataset=envelope.Dataset,SourceGenerationId=envelope.GenerationId}),token).ConfigureAwait(false);break;
            case DatasetPublicationKind.Quote:
                var quote=MessagePackSerializer.Deserialize<FuturesTickQuoteDataChangedEvent>(envelope.Payload,cancellationToken:token) with{SourceDataset=envelope.Dataset,SourceGenerationId=envelope.GenerationId};
                TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,quote);
                await publisher.PublishAsync(quote,new DeserializedQuoteLease(quote.QuoteData.Buffer,quote.QuoteCount),token).ConfigureAwait(false);break;
            case DatasetPublicationKind.MarketPrice:
                var price=TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,MessagePackSerializer.Deserialize<FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload,cancellationToken:token) with
                    {SourceDataset=envelope.Dataset,SourceGenerationId=envelope.GenerationId});
                if(recovered)await (publisher as IRecoveredFuturesItiPublisher??throw new InvalidOperationException("Original ITI recovery publisher is unavailable.")).PublishRecoveredAsync(price,token).ConfigureAwait(false);
                else await publisher.PublishAsync(price,token).ConfigureAwait(false);break;
            case DatasetPublicationKind.TradeReplayBatch:
                var replay=TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,MessagePackSerializer.Deserialize<FuturesTradeReplayBatchRealtimeEvent>(envelope.Payload,cancellationToken:token) with
                    {SourceDataset=envelope.Dataset,SourceGenerationId=envelope.GenerationId});
                if(recovered)await (publisher as IRecoveredFuturesItiPublisher??throw new InvalidOperationException("Original ITI replay publisher is unavailable.")).PublishRecoveredAsync(replay,token).ConfigureAwait(false);
                else await publisher.PublishAsync(replay,token).ConfigureAwait(false);break;
            case DatasetPublicationKind.SessionStatistics:
                await publisher.PublishAsync(TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(envelope,MessagePackSerializer.Deserialize<FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload,cancellationToken:token) with
                    {SourceDataset=envelope.Dataset,SourceGenerationId=envelope.GenerationId}),token).ConfigureAwait(false);break;
            default:throw new InvalidDataException("Unsupported retained source publication.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A diagnostic recorder failure must not abort realtime ingestion or recovery; this boundary isolates the observer.")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the existing public contract and named argument compatibility; changing parameter names or order would break callers.")]
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
