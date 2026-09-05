using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerPublisherTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);

    [Fact]
    public async Task Startup_drops_events_until_actual_native_generation_is_bound()
    {
        using var stream = new MemoryStream();
        var workerId = Guid.NewGuid();
        var nativeGeneration = Guid.NewGuid();
        await using var publisher = new PipeDatasetWorkerPublisher(stream, "GLBX.MDP3",
            ValueDate, workerId, 7);
        await publisher.StartAsync();

        await publisher.PublishAsync(Price());
        stream.Length.Should().Be(0);

        await publisher.BindGenerationAsync(nativeGeneration, CancellationToken.None);
        await publisher.PublishAsync(Price());
        stream.Position = 0;
        var publication = await DatasetPublicationFrameCodec.ReadAsync(stream, CancellationToken.None);
        publication.GenerationId.Should().Be(nativeGeneration);
        publication.WorkerInstanceId.Should().Be(workerId);
        publication.ManifestRevision.Should().Be(7);
        publication.PublicationSequence.Should().Be(1);
    }

    [Fact]
    public async Task Publisher_cannot_relabel_queued_events_by_changing_generation()
    {
        using var stream = new MemoryStream();
        await using var publisher = new PipeDatasetWorkerPublisher(stream, "GLBX.MDP3",
            ValueDate, Guid.NewGuid(), 1);
        await publisher.StartAsync();
        await publisher.BindGenerationAsync(Guid.NewGuid(), CancellationToken.None);

        var rebind = () => publisher.BindGenerationAsync(Guid.NewGuid(), CancellationToken.None).AsTask();

        await rebind.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Retired_publisher_drops_late_events_while_replacement_starts_sequence_at_one()
    {
        using var stream = new MemoryStream();
        var workerId = Guid.NewGuid();
        var oldGeneration = Guid.NewGuid();
        var nextGeneration = Guid.NewGuid();
        await using var oldPublisher = new PipeDatasetWorkerPublisher(stream, "GLBX.MDP3",
            ValueDate, workerId, 1);
        await oldPublisher.StartAsync();
        await oldPublisher.BindGenerationAsync(oldGeneration, CancellationToken.None);
        await oldPublisher.PublishAsync(Price());
        await oldPublisher.CloseAsync(CancellationToken.None);

        await using var replacement = new PipeDatasetWorkerPublisher(stream, "GLBX.MDP3",
            ValueDate, workerId, 2);
        await replacement.StartAsync();
        await replacement.BindGenerationAsync(nextGeneration, CancellationToken.None);
        await oldPublisher.PublishAsync(Price());
        await replacement.PublishAsync(Price());

        stream.Position = 0;
        var old = await DatasetPublicationFrameCodec.ReadAsync(stream, CancellationToken.None);
        var current = await DatasetPublicationFrameCodec.ReadAsync(stream, CancellationToken.None);
        old.GenerationId.Should().Be(oldGeneration);
        current.GenerationId.Should().Be(nextGeneration);
        current.ManifestRevision.Should().Be(2);
        current.PublicationSequence.Should().Be(1);
        stream.Position.Should().Be(stream.Length);
    }

    [Fact]
    public async Task Close_waits_for_in_flight_write_and_drops_waiting_events()
    {
        await using var stream = new GatedWriteStream();
        await using var publisher = new PipeDatasetWorkerPublisher(stream, "GLBX.MDP3",
            ValueDate, Guid.NewGuid(), 1);
        await publisher.StartAsync();
        await publisher.BindGenerationAsync(Guid.NewGuid(), CancellationToken.None);

        var first = publisher.PublishAsync(Price()).AsTask();
        await stream.WriteEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var queued = publisher.PublishAsync(Price()).AsTask();
        var closing = publisher.CloseAsync(CancellationToken.None).AsTask();
        closing.IsCompleted.Should().BeFalse();
        stream.AllowWrite.TrySetResult();
        await Task.WhenAll(first, queued, closing).WaitAsync(TimeSpan.FromSeconds(5));

        stream.Position = 0;
        var publication = await DatasetPublicationFrameCodec.ReadAsync(stream, CancellationToken.None);
        publication.PublicationSequence.Should().Be(1);
        stream.Position.Should().Be(stream.Length, "the queued event belongs to the retired publisher");
    }

    static FuturesMarketPriceUpdatedRealtimeEvent Price() => new()
    {
        Price = new FuturesMarketPriceSnapshot("ES20261218", 1, 1, AssetTypeId.Futures,
            ValueDate, null, new FuturesMarketTradeSnapshot(5500m, 1, 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)),
        UpdateSource = FuturesMarketPriceUpdateSource.Trade
    };

    sealed class GatedWriteStream : MemoryStream
    {
        public TaskCompletionSource WriteEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            WriteEntered.TrySetResult();
            await AllowWrite.Task.WaitAsync(cancellationToken);
            await base.WriteAsync(buffer, cancellationToken);
        }
    }
}
