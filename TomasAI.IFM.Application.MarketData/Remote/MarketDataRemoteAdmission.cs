using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.MarketData.Remote;
/// <summary>Actor-independent source fences; constructing a supervisor never constructs its publisher.</summary>
public sealed class MarketDataRemoteAdmission : IRealtimeSourceAdmission
{
    public DatasetWorkerAdmissionRegistry Futures { get; } = new();
    public DatasetWorkerAdmissionRegistry Options { get; } = new();
    public bool TryEnter(ActorSubject subject, string? dataset, Guid generation, out IDisposable? lease)
    {
        if (dataset is null || generation == Guid.Empty) return Futures.TryEnter(subject, dataset, generation, out lease);
        if (Options.TryGet(dataset, out var option) && option.GenerationId == generation) return Options.TryEnter(subject, dataset, generation, out lease);
        if (Futures.TryGet(dataset, out var future) && future.GenerationId == generation) return Futures.TryEnter(subject, dataset, generation, out lease);
        lease = null; return false;
    }
}
