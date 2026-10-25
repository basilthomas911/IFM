using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

/// <summary>Immutable status of the dedicated polling service.</summary>
public sealed record SupervisorPollingServiceStatus(
    SupervisorPollingServiceState State,
    string ThreadName,
    bool IsAlive,
    long CompletedCycles,
    long FailedCycles,
    long Overruns,
    DateTime? LastHeartbeatUtc);
