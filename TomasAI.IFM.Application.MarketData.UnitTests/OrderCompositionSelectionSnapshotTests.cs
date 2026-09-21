using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed partial class OrderCompositionWorkerTests
{
    [Fact]
    public async Task Selection_capture_exports_price_delta_and_original_IV_provenance_without_full_Greeks()
    {
        using var prices = Prices(); using var feed = new ChainFeed();
        var clock = new MutableClock();
        await using var runtime = Runtime(Factory(feed), prices, clock);
        var request = Request();
        Assert.True((await runtime.AcquireAsync(request, default)).Active);
        feed.Push(QuoteRecord(1));
        await Until(() => runtime.ReadSelection("ES-option-call") is { Delta: not null });
        var capture = new CompositionSnapshotRequest(Guid.NewGuid(), request.ScopeId, "Daily", Generation, At, At.AddSeconds(2), true)
            { SelectionOnly = true };
        var result = await new MarketCompositionSnapshotProvider(runtime, clock).CaptureAsync(capture, default);
        Assert.Null(result.Failure);
        Assert.Equal(2, result.Snapshot!.SchemaVersion);
        var item = Assert.Single(result.Snapshot.Instruments);
        Assert.Null(item.Valuation);
        Assert.NotNull(item.Instrument.Selection);
        Assert.True(double.IsFinite(item.Instrument.Selection!.Delta));
        Assert.Equal(1, item.Instrument.Selection.IvOption.Sequence);
        Assert.Equal(At, item.Instrument.Selection.IvCalculatedAtUtc);
        var frame = new DatasetWorkerControlFrame
        {
            Kind = DatasetWorkerMessageKind.CompositionSnapshotResult, WorkerInstanceId = Guid.NewGuid(),
            Dataset = "GLBX.MDP3", ValueDate = Date, GenerationId = Generation, CorrelationId = Guid.NewGuid(),
            Sequence = 1, BootstrapToken = new('a', 64), CompositionResult = result
        };
        using var stream = new MemoryStream();
        await DatasetWorkerFrameCodec.WriteAsync(stream, frame, 1024 * 1024, default);
        stream.Position = 0;
        var restored = await DatasetWorkerFrameCodec.ReadAsync(stream, 1024 * 1024, default);
        Assert.Equal(item.Instrument.Selection, Assert.Single(restored.CompositionResult!.Snapshot!.Instruments).Instrument.Selection);
        Assert.True(MessagePackSerializer.Deserialize<CompositionSnapshotRequest>(MessagePackSerializer.Serialize(capture)).SelectionOnly);
        clock.Now = At.AddSeconds(2);
        var stale = await new MarketCompositionSnapshotProvider(runtime, clock).CaptureAsync(
            capture with { EvaluatedAtUtc = clock.Now, DeadlineUtc = clock.Now.AddSeconds(2) }, default);
        Assert.NotNull(stale.Failure);
        Assert.Null(stale.Snapshot);
    }
}
