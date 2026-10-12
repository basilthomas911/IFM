using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Describes the terminal result of one startup activity.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum ApplicationStartupActivityOutcome
{
    Started = 1,
    AlreadySatisfied = 2,
    ScheduledStopped = 3,
    Degraded = 4,
    Failed = 5,
    SkippedDependency = 6
}
