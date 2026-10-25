using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Describes the aggregate state of the latest startup workflow.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum ApplicationLifecycleState
{
    Bootstrapped = 1,
    Starting = 2,
    Running = 3,
    Degraded = 4,
    Failed = 5,
    ScheduledStopped = 6
}
