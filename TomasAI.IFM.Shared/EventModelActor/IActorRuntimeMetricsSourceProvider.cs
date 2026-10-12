using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Publishes a stable read-only actor-source list for one collection cycle.</summary>
public interface IActorRuntimeMetricsSourceProvider
{
    IReadOnlyList<IActorRuntimeMetricsSource> CaptureActorMetricsSources();
}
