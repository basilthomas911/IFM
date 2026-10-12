using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Policy;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health.Evaluation;

/// <summary>One immutable entity-mailbox observation used by the baseline health policy.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct SupervisorActorThreadHealthObservation(
    int Depth, int Capacity, long Rejected, long Dequeued, long Completed, bool IsProcessing,
    TimeSpan ProcessingDuration, long Generation, bool IsAdmissionOpen = true);
