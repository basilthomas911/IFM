using System.IO.Pipes;
using NATS.Client.Core;
using MessagePack;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Host.Worker;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionTradeRetentionTests
{
    static OptionTradeEvidence Evidence() => new(new("GLBX.MDP3", 1, 10, "ES-option-call",
        new(2026, 9, 8), 110, 1, 99, 1788883200000000123, 1788883200000000456, Generation, "fixture-call"),
        null, null, At, null, new("PricingContextUnavailable", "Context", "ES-option-call", "Test fixture."));

    [Fact(Timeout = 15000)]
    public async Task Host_relay_delivers_quotes_before_slow_evidence_and_preserves_delivery_sequence()
    {
        await using var manager = new TomasAI.IFM.Framework.Messaging.NatsJetStream.NatsConnectionManager();
        var connection = new MarketDataHostConnection(manager, "nats://127.0.0.1:4222", "retentiontest" + Guid.NewGuid().ToString("N"));
        var worker = Guid.NewGuid();
        var store = new GatedStore();
        var relay = new TomasAI.IFM.Application.MarketData.Host.WorkerPublicationRelay(connection, "Options", worker, store);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = await connection.GetClientAsync(deadline.Token);
        await using var subscription = await client.Connection.SubscribeCoreAsync<DatasetPublicationEnvelope>(
            connection.Publications("Options", worker), serializer: TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers.NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default,
            cancellationToken: deadline.Token);
        await client.PingAsync(deadline.Token);
        var envelope = new DatasetPublicationEnvelope
        {
            Dataset = "GLBX.MDP3", ValueDate = new(2026, 9, 8), WorkerInstanceId = worker,
            GenerationId = Generation, ManifestRevision = 1, PublicationSequence = 1,
            Kind = DatasetPublicationKind.OptionTradeEvidence, Payload = MessagePackSerializer.Serialize(Evidence())
        };
        var persisting = relay.PublishAsync(envelope, deadline.Token).AsTask();
        await relay.PublishAsync(envelope with { Kind = DatasetPublicationKind.OptionQuoteObservation, PublicationSequence = 2, Payload = [1] }, deadline.Token);
        var quote = await subscription.Msgs.ReadAsync(deadline.Token);
        Assert.Equal(DatasetPublicationKind.OptionQuoteObservation, quote.Data!.Kind);
        Assert.False(persisting.IsCompleted);
        store.Completed.SetResult();
        await persisting;
        var evidence = await subscription.Msgs.ReadAsync(deadline.Token);
        Assert.Equal(DatasetPublicationKind.OptionTradeEvidence, evidence.Data!.Kind);
        Assert.True(evidence.Data.PublicationSequence > quote.Data.PublicationSequence);
        Assert.Equal(envelope.Payload, evidence.Data.Payload);
        relay.Close();
    }

    [Fact]
    public async Task Quote_publication_continues_while_evidence_waits_for_retention_acknowledgment()
    {
        using var publication = new MemoryStream();
        using var ackServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var ackClient = new AnonymousPipeClientStream(PipeDirection.In, ackServer.ClientSafePipeHandle);
        await using var writer = new PipeDatasetWorkerPublisher(publication, "GLBX.MDP3", new(2026, 9, 8), Guid.NewGuid(), 1, ackClient);
        await writer.StartAsync(); await writer.BindGenerationAsync(Generation, default);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var evidence = writer.WriteAsync(Evidence(), deadline.Token).AsTask();
        Assert.False(evidence.IsCompleted);
        await writer.PublishAsync(new TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent(), deadline.Token).AsTask().WaitAsync(deadline.Token);
        publication.Position = 0;
        var first = await DatasetPublicationFrameCodec.ReadAsync(publication, deadline.Token);
        var second = await DatasetPublicationFrameCodec.ReadAsync(publication, deadline.Token);
        Assert.Equal(DatasetPublicationKind.OptionTradeEvidence, first.Kind);
        Assert.Equal(DatasetPublicationKind.MarketPrice, second.Kind);
        Assert.False(evidence.IsCompleted);
        await OptionTradeAcknowledgment.WriteAsync(ackServer, first.PublicationSequence, first.GenerationId, true, deadline.Token);
        await evidence;
    }

    [Fact]
    public async Task Worker_does_not_complete_retention_until_host_store_completes_and_acknowledges()
    {
        using var publication = new MemoryStream();
        using var ackServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var ackClient = new AnonymousPipeClientStream(PipeDirection.In, ackServer.ClientSafePipeHandle);
        await using var writer = new PipeDatasetWorkerPublisher(publication, "GLBX.MDP3", new(2026, 9, 8), Guid.NewGuid(), 1, ackClient);
        await writer.StartAsync(); await writer.BindGenerationAsync(Generation, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var retained = writer.WriteAsync(Evidence(), timeout.Token).AsTask();
        Assert.False(retained.IsCompleted);
        publication.Position = 0;
        var envelope = await DatasetPublicationFrameCodec.ReadAsync(publication, timeout.Token);
        var admissions = new DatasetWorkerAdmissionRegistry();
        admissions.Admit(new(envelope.Dataset, envelope.ValueDate, envelope.WorkerInstanceId, envelope.GenerationId, 1));
        var store = new GatedStore();
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        var ingress = new DatasetPublicationIngress(admissions, publisher,
            Substitute.For<IMarketDataOperationsRecorder>(), optionTrades: store);
        var accepting = ingress.AcceptAsync(envelope, timeout.Token).AsTask();
        Assert.False(accepting.IsCompleted);
        Assert.False(retained.IsCompleted);
        Assert.Empty(publisher.ReceivedCalls());
        store.Completed.SetResult();
        Assert.True(await accepting);
        await OptionTradeAcknowledgment.WriteAsync(ackServer, envelope.PublicationSequence, Generation, true, timeout.Token);
        await retained;
        Assert.Equal(Evidence(), store.Value);
        await publisher.Received(1).PublishAsync(
            Arg.Is<TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataChangedEvent>(tick =>
                tick.TickDataId.ContractId == Evidence().Source.ContractId &&
                tick.TradeData.Price == Evidence().Source.Price &&
                tick.TradeData.SourceSequence == 99 && tick.SourceGenerationId == Generation),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Rejection_or_wrong_generation_never_acknowledges_a_trade(bool retained, bool wrongGeneration)
    {
        using var output = new MemoryStream(); using var acknowledgments = new MemoryStream();
        await OptionTradeAcknowledgment.WriteAsync(acknowledgments, 1, wrongGeneration ? Guid.NewGuid() : Generation, retained, default);
        acknowledgments.Position = 0;
        await using var writer = new PipeDatasetWorkerPublisher(output, "GLBX.MDP3", new(2026, 9, 8), Guid.NewGuid(), 1, acknowledgments);
        await writer.StartAsync(); await writer.BindGenerationAsync(Generation, default);
        await Assert.ThrowsAsync<InvalidDataException>(() => writer.WriteAsync(Evidence(), default).AsTask());
        Assert.False(writer.IsRunning);
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(Evidence(), default).AsTask());
    }

    [Fact]
    public async Task Retired_generation_never_reaches_the_durable_writer()
    {
        var store = new GatedStore();
        var ingress = new DatasetPublicationIngress(new(), Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>(), optionTrades: store);
        Assert.False(await ingress.AcceptAsync(new()
        {
            Dataset = "GLBX.MDP3",
            ValueDate = new(2026, 9, 8),
            WorkerInstanceId = Guid.NewGuid(),
            GenerationId = Generation,
            ManifestRevision = 1,
            PublicationSequence = 1,
            Kind = DatasetPublicationKind.OptionTradeEvidence,
            Payload = MessagePackSerializer.Serialize(Evidence())
        }));
        Assert.Null(store.Value);
    }
    [Fact]
    public void Retransmission_preserves_event_identity_and_native_source_timestamps()
    {
        var evidence = Evidence();
        var first = evidence.ToRealtimeEvent();
        var replay = (evidence with { Source = evidence.Source with
        { ReceiveNanoseconds = evidence.Source.ReceiveNanoseconds + 1000, GenerationId = Guid.NewGuid() } }).ToRealtimeEvent();
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.CommandId, replay.CommandId);
        Assert.Equal(evidence.Source.EventNanoseconds, first.TradeData.EventTimestampNanoseconds);
        Assert.Equal(evidence.Source.ReceiveNanoseconds, first.TradeData.ReceiveTimestampNanoseconds);
        Assert.Equal(evidence.Source.ValueDate, first.EntityId.ValueDate);
    }

    sealed class GatedStore : IOptionTradeEvidenceWriter
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public OptionTradeEvidence? Value;
        public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken token)
        { Value = evidence; await Completed.Task.WaitAsync(token); }
    }
}
