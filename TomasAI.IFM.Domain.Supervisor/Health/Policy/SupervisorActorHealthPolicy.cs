namespace TomasAI.IFM.Domain.Supervisor.Health.Policy;

/// <summary>Baseline actor-thread health timings applied until a later actor-specific override is approved.</summary>
public sealed record SupervisorActorHealthPolicy
{
    public TimeSpan CollectionInterval { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan WarningInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan DegradedAfter { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan RestartRequiredAfter { get; init; } = TimeSpan.FromMinutes(15);
    public double ElevatedUtilization { get; init; } = 0.75;
    public double CriticalUtilization { get; init; } = 0.90;
    public TimeSpan PressureConfirmation { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan NoProgressAfter { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan HandlerWarningAfter { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan SaturationRecoveryAfter { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan HealthRecoveryAfter { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan PostRestartObservation { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Validates the baseline policy.</summary>
    public SupervisorActorHealthPolicy Validate()
    {
        if (CollectionInterval <= TimeSpan.Zero) throw new InvalidOperationException("CollectionInterval must be positive.");
        if (WarningInterval < CollectionInterval) throw new InvalidOperationException("WarningInterval cannot be shorter than collection cadence.");
        if (DegradedAfter < WarningInterval) throw new InvalidOperationException("DegradedAfter cannot precede the warning interval.");
        if (RestartRequiredAfter < DegradedAfter) throw new InvalidOperationException("RestartRequiredAfter cannot precede degradation.");
        if (ElevatedUtilization is <= 0 or >= 1) throw new InvalidOperationException("ElevatedUtilization must be between zero and one.");
        if (CriticalUtilization <= ElevatedUtilization || CriticalUtilization > 1) throw new InvalidOperationException("CriticalUtilization must exceed elevated utilization and not exceed one.");
        if (PressureConfirmation <= TimeSpan.Zero || NoProgressAfter <= TimeSpan.Zero || HandlerWarningAfter <= TimeSpan.Zero)
            throw new InvalidOperationException("Pressure and progress intervals must be positive.");
        if (SaturationRecoveryAfter <= TimeSpan.Zero || HealthRecoveryAfter < SaturationRecoveryAfter)
            throw new InvalidOperationException("Health recovery must not precede saturation recovery.");
        return this;
    }
}
