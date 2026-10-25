namespace TomasAI.IFM.Shared.EventModelActor;
/// <summary>Operational capability exposed to actors without depending on its runtime implementation.</summary>
public interface ISupervisorRuntimeContext : IActorFailureSink, IActorRuntimeMetricsSourceProvider
{
    void RegisterWorker(ISupervisorWorkerMetricsSource worker);
    void RemoveWorker(ISupervisorWorkerMetricsSource worker);
    void RegisterProjector(ISupervisorProjectorMetricsSource projector);
    SupervisorRuntimeSnapshot CaptureSnapshot(DateTime? fromUtc = null, DateTime? toUtc = null);
}
