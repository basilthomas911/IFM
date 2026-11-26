using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.MarketData.Remote;
/// <summary>Actor-independent source fences; constructing a supervisor never constructs its publisher.</summary>
public sealed class MarketDataRemoteAdmission : IRealtimeSourceAdmission
{
    readonly TaskCompletionSource publicationsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource futuresChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task WaitForPublicationsAsync(CancellationToken token) => publicationsReady.Task.WaitAsync(TimeSpan.FromSeconds(30), token);
    internal void MarkPublicationsReady() => publicationsReady.TrySetResult();
    internal void NotifyFuturesChanged() => Interlocked.Exchange(ref futuresChanged,
        new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    bool Matches(DatasetPublicationEnvelope frame) => Futures.TryGet(frame.Dataset, out var identity)
        && identity.WorkerInstanceId == frame.WorkerInstanceId && identity.GenerationId == frame.GenerationId
        && identity.ValueDate == frame.ValueDate && identity.ManifestRevision == frame.ManifestRevision;
    internal Task WaitForFuturesAsync(DatasetPublicationEnvelope frame, CancellationToken token)
        => Matches(frame) ? Task.CompletedTask : AwaitFuturesAsync(frame, token);
    async Task AwaitFuturesAsync(DatasetPublicationEnvelope frame, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        while (true)
        {
            var changed = Volatile.Read(ref futuresChanged).Task;
            if (Matches(frame)) return;
            await changed.WaitAsync(deadline.Token).ConfigureAwait(false);
        }
    }
    internal async ValueTask WaitForSourceAsync(DatasetPublicationEnvelope frame,bool option,CancellationToken token)
    {
        var registry=option?Options:Futures;
        bool Matches()=>registry.TryGet(frame.Dataset,out var current)&&current.WorkerInstanceId==frame.WorkerInstanceId&&current.GenerationId==frame.GenerationId&&current.ValueDate==frame.ValueDate&&current.ManifestRevision==frame.ManifestRevision;
        while(!Matches()){var changed=Volatile.Read(ref futuresChanged).Task;if(Matches())return;await changed.WaitAsync(token).ConfigureAwait(false);}
    }
    public DatasetWorkerAdmissionRegistry Futures { get; } = new();
    public DatasetWorkerAdmissionRegistry Options { get; } = new();
    public bool TryEnter(ActorSubject subject, string? dataset, Guid generation, out IDisposable? lease)
    {
        if (dataset is null || generation == Guid.Empty) return Futures.TryEnter(subject, dataset, generation, out lease);
        if (Options.TryGet(dataset, out var option) && option.GenerationId == generation) return Options.TryEnter(subject, dataset, generation, out lease);
        if (Futures.TryGet(dataset, out var future) && future.GenerationId == generation) return Futures.TryEnter(subject, dataset, generation, out lease);
        if(Options.TryEnterDurableRecovery(dataset,generation,out lease))return true;
        return Futures.TryEnterDurableRecovery(dataset,generation,out lease);
    }
}
