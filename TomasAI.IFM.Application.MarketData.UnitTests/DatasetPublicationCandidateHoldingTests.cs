using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetPublicationCandidateHoldingTests
{
    [Fact]
    public async Task Candidate_is_bounded_and_never_published_before_downstream_admission()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        var ingress = new DatasetPublicationIngress(admissions, publisher,
            Substitute.For<IMarketDataOperationsRecorder>());
        var identity = Admission();
        var buffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(2);
        ingress.StartCandidateHolding(identity, buffer);

        Assert.True(await ingress.AcceptAsync(Envelope(identity, 1)));
        Assert.True(await ingress.AcceptAsync(Envelope(identity, 2)));
        Assert.False(await ingress.AcceptAsync(Envelope(identity, 3)));
        Assert.Equal(new(2, 2, 2, 1), buffer.Capture());
        Assert.True(buffer.TryTake(out var first));
        Assert.Equal(1, first.PublicationSequence);
        Assert.False(await ingress.AcceptAsync(Envelope(identity, 2)));
        Assert.True(await ingress.AcceptAsync(Envelope(identity, 4)));
        Assert.Equal(new(2, 2, 3, 1), buffer.Capture());
        Assert.False(admissions.TryGet(identity.Dataset, out _));
        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>());

        ingress.StopCandidateHolding(identity);
        Assert.False(await ingress.AcceptAsync(Envelope(identity, 5)));
    }

    [Fact]
    public async Task Other_generation_cannot_enter_candidate_buffer()
    {
        var ingress = new DatasetPublicationIngress(new(),
            Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>());
        var candidate = Admission();
        var buffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(1);
        ingress.StartCandidateHolding(candidate, buffer);

        Assert.False(await ingress.AcceptAsync(Envelope(candidate with { GenerationId = Guid.NewGuid() }, 1)));
        Assert.Equal(0, buffer.Capture().Count);
        ingress.StopCandidateHolding(candidate);
    }

    [Fact]
    public void Candidate_cannot_start_until_previous_generation_is_fenced()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var previous = Admission();
        admissions.Admit(previous);
        var ingress = new DatasetPublicationIngress(admissions,
            Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>());
        var candidate = previous with { GenerationId = Guid.NewGuid() };

        Assert.Throws<InvalidOperationException>(() => ingress.StartCandidateHolding(candidate,
            new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(1)));

        admissions.Close(previous.Dataset, previous.GenerationId);
        ingress.StartCandidateHolding(candidate,
            new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(1));
        ingress.StopCandidateHolding(candidate);
    }

    [Fact]
    public async Task Each_required_dataset_has_an_independent_bounded_candidate_buffer()
    {
        var ingress = new DatasetPublicationIngress(new(),
            Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>());
        var glbx = Admission();
        var other = Admission() with { Dataset = "OPRA.PILLAR" };
        var glbxBuffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(1);
        var otherBuffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(1);
        ingress.StartCandidateHolding(glbx, glbxBuffer);
        ingress.StartCandidateHolding(other, otherBuffer);

        Assert.True(await ingress.AcceptAsync(Envelope(glbx, 1)));
        Assert.True(await ingress.AcceptAsync(Envelope(other, 1)));
        Assert.False(await ingress.AcceptAsync(Envelope(glbx, 2)));
        Assert.False(await ingress.AcceptAsync(Envelope(other, 2)));
        Assert.Equal(1, glbxBuffer.Capture().Count);
        Assert.Equal(1, otherBuffer.Capture().Count);
        Assert.Equal(1, glbxBuffer.Capture().Dropped);
        Assert.Equal(1, otherBuffer.Capture().Dropped);

        ingress.StopCandidateHolding(glbx);
        ingress.StopCandidateHolding(other);
    }

    [Fact]
    public async Task Candidate_probe_publishes_only_one_held_tick_and_keeps_other_traffic_fenced()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        var ingress = new DatasetPublicationIngress(admissions, publisher,
            Substitute.For<IMarketDataOperationsRecorder>());
        var identity = Admission();
        var buffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(4);
        ingress.StartCandidateHolding(identity, buffer);
        admissions.BeginProbe(identity);
        Assert.True(await ingress.AcceptAsync(Envelope(identity, 1)));
        var trade = new FuturesTickTradeDataChangedEvent
        {
            Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                FuturesTickTradeDataChangedEvent.Verb, "ESZ26"),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), Dataset = identity.Dataset,
            ReceivedOn = DateTime.UtcNow
        };
        Assert.True(await ingress.AcceptAsync(Envelope(identity, 2) with
        {
            Kind = DatasetPublicationKind.Trade,
            Payload = MessagePackSerializer.Serialize(trade)
        }));
        Assert.True(await ingress.PublishHeldCandidateTickProbeAsync(identity));
        Assert.Equal(1, buffer.Capture().Count);
        Assert.True(buffer.TryTake(out var remaining));
        Assert.Equal(DatasetPublicationKind.MarketPrice, remaining.Kind);
        await publisher.Received(1).PublishAsync(
            Arg.Is<FuturesTickTradeDataChangedEvent>(value =>
                value.SourceDataset == identity.Dataset && value.SourceGenerationId == identity.GenerationId),
            Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().PublishAsync(
            Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>());
        Assert.False(admissions.TryGet(identity.Dataset, out _));
        await admissions.EndProbeAsync(identity, TimeSpan.FromSeconds(1));
        ingress.StopCandidateHolding(identity);
    }

    [Fact]
    public void Taking_matching_held_record_preserves_other_order_and_predicate_failure_preserves_all()
    {
        var buffer = new DatabentoRecoveryHoldingBuffer<int>(4);
        Assert.True(buffer.TryHold(1));
        Assert.True(buffer.TryHold(2));
        Assert.True(buffer.TryHold(3));
        Assert.Throws<InvalidOperationException>(() => buffer.TryTakeWhere(
            _ => throw new InvalidOperationException("Injected predicate failure"), out _));
        Assert.True(buffer.TryTakeWhere(value => value == 2, out var selected));
        Assert.Equal(2, selected);
        Assert.True(buffer.TryTake(out var first));
        Assert.True(buffer.TryTake(out var last));
        Assert.Equal(1, first);
        Assert.Equal(3, last);
    }

    [Fact]
    public async Task Promotion_releases_no_dataset_until_every_candidate_is_admitted()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var ingress = new DatasetPublicationIngress(admissions,
            Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>());
        var first = Admission();
        var second = Admission() with { Dataset = "OPRA.PILLAR" };
        var firstBuffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(3);
        var secondBuffer = new DatabentoRecoveryHoldingBuffer<DatasetPublicationEnvelope>(3);
        ingress.StartCandidateHolding(first, firstBuffer);
        ingress.StartCandidateHolding(second, secondBuffer);
        admissions.Admit(first);

        Assert.Throws<InvalidOperationException>(() =>
            ingress.PromoteCandidateHoldings([first, second]));
        Assert.True(await ingress.AcceptAsync(Envelope(first, 1)));
        Assert.True(await ingress.AcceptAsync(Envelope(second, 1)));
        Assert.Equal(1, firstBuffer.Capture().Count);
        Assert.Equal(1, secondBuffer.Capture().Count);

        admissions.Admit(second);
        ingress.PromoteCandidateHoldings([first, second]);
        Assert.Throws<InvalidOperationException>(() => ingress.StopCandidateHolding(first));
        Assert.Throws<InvalidOperationException>(() => ingress.StopCandidateHolding(second));
    }

    static DatasetWorkerAdmission Admission() => new("GLBX.MDP3", new DateOnly(2026, 9, 30),
        Guid.NewGuid(), Guid.NewGuid(), 1);

    static DatasetPublicationEnvelope Envelope(DatasetWorkerAdmission identity, long sequence) => new()
    {
        Dataset = identity.Dataset,
        ValueDate = identity.ValueDate,
        WorkerInstanceId = identity.WorkerInstanceId,
        GenerationId = identity.GenerationId,
        ManifestRevision = identity.ManifestRevision,
        PublicationSequence = sequence,
        Kind = DatasetPublicationKind.MarketPrice,
        Payload = [1]
    };
}
