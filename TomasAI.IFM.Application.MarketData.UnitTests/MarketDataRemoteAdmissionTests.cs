using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Remote;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataRemoteAdmissionTests
{
    [Fact]
    public async Task Feed_start_barrier_waits_until_subscription_is_flushed()
    {
        var admission = new MarketDataRemoteAdmission();
        var waiting = admission.WaitForPublicationsAsync(default);
        Assert.False(waiting.IsCompleted);
        admission.MarkPublicationsReady();
        await waiting.WaitAsync(TimeSpan.FromSeconds(1));
        await admission.WaitForPublicationsAsync(default);
    }

    [Fact]
    public async Task Startup_prefix_waits_for_exact_manifest_and_then_uses_completed_fast_path()
    {
        var admission = new MarketDataRemoteAdmission();
        var frame = new DatasetPublicationEnvelope { Dataset = "GLBX.MDP3", ValueDate = new(2026, 11, 23),
            WorkerInstanceId = Guid.NewGuid(), GenerationId = Guid.NewGuid(), ManifestRevision = 2,
            PublicationSequence = 1, Kind = DatasetPublicationKind.MarketPrice, Payload = [1] };
        var waiting = admission.WaitForFuturesAsync(frame, default);
        Assert.False(waiting.IsCompleted);
        admission.Futures.Admit(new(frame.Dataset, frame.ValueDate, frame.WorkerInstanceId, frame.GenerationId, 1));
        admission.NotifyFuturesChanged();
        Assert.False(waiting.IsCompleted);
        admission.Futures.Admit(new(frame.Dataset, frame.ValueDate, frame.WorkerInstanceId, frame.GenerationId, 2));
        admission.NotifyFuturesChanged();
        await waiting.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Same(Task.CompletedTask, admission.WaitForFuturesAsync(frame, default));
    }
}
