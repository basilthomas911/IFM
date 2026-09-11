namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

/// <summary>Controls the end-to-end live-pipeline hard-reset boundary.</summary>
public sealed record LivePipelineMonitorOptions
{
    public TimeSpan HardResetDelay { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RecoveryObservationWindow { get; init; } = TimeSpan.FromMinutes(5);
    public bool ForceOneHardResetAfterStartup { get; init; }

    public LivePipelineMonitorOptions Validate()
    {
        if (HardResetDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(HardResetDelay));
        if (RecoveryObservationWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RecoveryObservationWindow));
        return this;
    }
}