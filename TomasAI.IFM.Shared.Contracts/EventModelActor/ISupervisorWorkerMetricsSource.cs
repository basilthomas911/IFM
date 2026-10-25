using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public interface ISupervisorWorkerMetricsSource
{
    int SupervisorWorkerId { get; }
    SupervisorWorkerSnapshot CaptureSupervisorSnapshot();
}
