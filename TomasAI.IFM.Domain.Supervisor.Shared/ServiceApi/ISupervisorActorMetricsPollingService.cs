using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

/// <summary>Controls the dedicated actor-metrics polling thread.</summary>
public interface ISupervisorActorMetricsPollingService
{
    SupervisorPollingServiceState State { get; }
    SupervisorPollingServiceStatus CaptureStatus();
    void Start();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing published actor member name.")]
    bool Stop(TimeSpan timeout);
}
