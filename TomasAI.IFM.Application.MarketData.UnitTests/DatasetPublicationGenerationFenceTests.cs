using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetPublicationGenerationFenceTests
{
    [Fact]
    public async Task Closing_generation_cancels_already_queued_output_but_not_the_other_dataset()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var es = Admission("GLBX.MDP3");
        var vx = Admission("XCBF.PITCH");
        admissions.Admit(es);
        admissions.Admit(vx);
        var queued = new List<CancellationToken>();
        var ingress = CreateIngress(admissions, queued);
        Assert.True(await ingress.AcceptAsync(Price(es, "ES20260918")));
        Assert.True(await ingress.AcceptAsync(Price(vx, "VX20260916")));

        // AcceptAsync has returned, but the real publisher has not necessarily transmitted yet.
        admissions.Close(es.Dataset, es.GenerationId);

        Assert.True(queued[0].IsCancellationRequested);
        Assert.False(queued[1].IsCancellationRequested);
        var replacement = es with { GenerationId = Guid.NewGuid() };
        admissions.Admit(replacement);
        Assert.True(await ingress.AcceptAsync(Price(replacement, "ES20260918")));
        Assert.False(queued[2].IsCancellationRequested);
        Assert.False(await ingress.AcceptAsync(Price(es, "ES20260918", 2)));
        admissions.Close(replacement.Dataset, replacement.GenerationId);
        admissions.Close(vx.Dataset, vx.GenerationId);
    }

    [Fact]
    public async Task Direct_readmission_cancels_replaced_generation_even_without_a_prior_close()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var original = Admission("GLBX.MDP3");
        admissions.Admit(original);
        var queued = new List<CancellationToken>();
        var ingress = CreateIngress(admissions, queued);
        Assert.True(await ingress.AcceptAsync(Price(original, "ES20260918")));
        var replacement = original with { GenerationId = Guid.NewGuid(), ManifestRevision = 2 };

        admissions.Admit(replacement);

        Assert.True(queued[0].IsCancellationRequested);
        Assert.True(await ingress.AcceptAsync(Price(replacement, "ES20260918")));
        Assert.False(queued[1].IsCancellationRequested);
        admissions.Close(original.Dataset, original.GenerationId);
        Assert.False(queued[1].IsCancellationRequested);
        admissions.Close(replacement.Dataset, replacement.GenerationId);
    }

    [Fact]
    public void Duplicate_admission_preserves_sequence_fence_and_queued_token()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = Admission("GLBX.MDP3");
        admissions.Admit(identity);
        Assert.True(admissions.TryAccept(identity, 10, out var firstToken));

        admissions.Admit(identity);

        Assert.False(firstToken.IsCancellationRequested);
        Assert.False(admissions.TryAccept(identity, 10));
        Assert.True(admissions.TryAccept(identity, 11, out var secondToken));
        Assert.Equal(firstToken, secondToken);
        admissions.Close(identity.Dataset, identity.GenerationId);
        Assert.True(firstToken.IsCancellationRequested);
        Assert.True(secondToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Expected_generation_cancellation_during_publish_is_a_rejection_not_a_reader_fault()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = Admission("GLBX.MDP3");
        admissions.Admit(identity);
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        publisher.PublishAsync(Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.ArgAt<CancellationToken>(1);
                admissions.Close(identity.Dataset, identity.GenerationId);
                return ValueTask.FromCanceled(token);
            });
        var ingress = new DatasetPublicationIngress(admissions, publisher,
            Substitute.For<IMarketDataOperationsRecorder>());

        Assert.False(await ingress.AcceptAsync(Price(identity, "ES20260918")));
    }

    [Fact]
    public async Task Caller_cancelled_before_admission_does_not_consume_publication_sequence()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = Admission("GLBX.MDP3");
        admissions.Admit(identity);
        var queued = new List<CancellationToken>();
        var ingress = CreateIngress(admissions, queued);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ingress.AcceptAsync(Price(identity, "ES20260918"), new CancellationToken(true)));

        Assert.Empty(queued);
        Assert.True(admissions.TryAccept(identity, 1));
        admissions.Close(identity.Dataset, identity.GenerationId);
    }

    [Fact]
    public async Task Caller_lifetime_cannot_disconnect_generation_fencing_after_enqueue_returns()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = Admission("GLBX.MDP3");
        admissions.Admit(identity);
        var queued = new List<CancellationToken>();
        var ingress = CreateIngress(admissions, queued);
        using (var caller = new CancellationTokenSource())
            Assert.True(await ingress.AcceptAsync(Price(identity, "ES20260918"), caller.Token));

        admissions.Close(identity.Dataset, identity.GenerationId);

        Assert.True(queued.Single().IsCancellationRequested);
    }

    [Fact]
    public async Task A_blocking_external_cancellation_callback_cannot_block_admission_close()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = Admission("GLBX.MDP3");
        admissions.Admit(identity);
        Assert.True(admissions.TryAccept(identity, 1, out var token));
        using var release = new ManualResetEventSlim();
        var callbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            callbackFinished.TrySetResult();
        });
        try
        {
            await Task.Run(() => admissions.Close(identity.Dataset, identity.GenerationId))
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(token.IsCancellationRequested);
            Assert.False(admissions.TryGet(identity.Dataset, out _));
        }
        finally
        {
            release.Set();
            await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    static DatasetPublicationIngress CreateIngress(
        DatasetWorkerAdmissionRegistry admissions, List<CancellationToken> queued)
    {
        var publisher = Substitute.For<ITickAggregationEventPublisher>();
        publisher.PublishAsync(Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                queued.Add(call.ArgAt<CancellationToken>(1));
                return ValueTask.CompletedTask;
            });
        return new DatasetPublicationIngress(admissions, publisher,
            Substitute.For<IMarketDataOperationsRecorder>());
    }

    static DatasetWorkerAdmission Admission(string dataset) =>
        new(dataset, new DateOnly(2026, 9, 4), Guid.NewGuid(), Guid.NewGuid(), 1);

    static DatasetPublicationEnvelope Price(DatasetWorkerAdmission identity, string contractId, long sequence = 1) => new()
    {
        Dataset = identity.Dataset,
        ValueDate = identity.ValueDate,
        WorkerInstanceId = identity.WorkerInstanceId,
        GenerationId = identity.GenerationId,
        ManifestRevision = identity.ManifestRevision,
        PublicationSequence = sequence,
        Kind = DatasetPublicationKind.MarketPrice,
        Payload = MessagePackSerializer.Serialize(new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Price = new FuturesMarketPriceSnapshot(contractId, 42, 1, AssetTypeId.Futures,
                identity.ValueDate, null, null)
        })
    };
}
