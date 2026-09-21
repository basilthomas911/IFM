using System.IO.Pipes;
using MessagePack;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionTradeRetentionTests
{
    static OptionTradeEvidence Evidence() => new(new("GLBX.MDP3", 1, 10, "ES-option-call",
        new(2026, 9, 8), 110, 1, 99, 1788883200000000123, 1788883200000000456, Generation, "fixture-call"),
        null, null, At, null, new("PricingContextUnavailable", "Context", "ES-option-call", "Test fixture."));

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
        var ingress = new DatasetPublicationIngress(admissions, Substitute.For<ITickAggregationEventPublisher>(),
            Substitute.For<IMarketDataOperationsRecorder>(), optionTrades: store);
        var accepting = ingress.AcceptAsync(envelope, timeout.Token).AsTask();
        Assert.False(accepting.IsCompleted);
        Assert.False(retained.IsCompleted);
        store.Completed.SetResult();
        Assert.True(await accepting);
        await OptionTradeAcknowledgment.WriteAsync(ackServer, envelope.PublicationSequence, Generation, true, timeout.Token);
        await retained;
        Assert.Equal(Evidence(), store.Value);
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
            Dataset = "GLBX.MDP3", ValueDate = new(2026, 9, 8), WorkerInstanceId = Guid.NewGuid(),
            GenerationId = Generation, ManifestRevision = 1, PublicationSequence = 1,
            Kind = DatasetPublicationKind.OptionTradeEvidence, Payload = MessagePackSerializer.Serialize(Evidence())
        }));
        Assert.Null(store.Value);
    }
    sealed class GatedStore : IOptionTradeEvidenceWriter
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public OptionTradeEvidence? Value;
        public async ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken token)
        { Value = evidence; await Completed.Task.WaitAsync(token); }
    }
}
