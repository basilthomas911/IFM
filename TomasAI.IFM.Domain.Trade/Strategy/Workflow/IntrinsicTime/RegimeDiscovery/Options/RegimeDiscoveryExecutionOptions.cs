namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;

/// <summary>Configures the hard wall-clock boundary for one Regime Discovery execution.</summary>
public sealed class RegimeDiscoveryExecutionOptions
{
    /// <summary>Configuration section below AppSettings.</summary>
    public const string SectionName = "AppSettings:RegimeDiscoveryExecution";
    /// <summary>Default maximum execution duration pending workload qualification.</summary>
    public static readonly TimeSpan DefaultMaximumExecutionDuration = TimeSpan.FromMinutes(2);
    /// <summary>Smallest supported hard timeout.</summary>
    public static readonly TimeSpan MinimumExecutionDuration = TimeSpan.FromSeconds(1);
    /// <summary>Largest supported hard timeout.</summary>
    public static readonly TimeSpan MaximumAllowedExecutionDuration = TimeSpan.FromHours(1);

    /// <summary>Gets or sets the fixed maximum duration persisted by Strategy Workflow.</summary>
    public TimeSpan MaximumExecutionDuration { get; set; } = DefaultMaximumExecutionDuration;

    /// <summary>Rejects missing, non-positive, or operationally unbounded timeout values at startup.</summary>
    public void Validate()
    {
        if (MaximumExecutionDuration < MinimumExecutionDuration ||
            MaximumExecutionDuration > MaximumAllowedExecutionDuration)
            throw new ArgumentOutOfRangeException(
                nameof(MaximumExecutionDuration),
                MaximumExecutionDuration,
                $"Regime Discovery maximum execution duration must be between " +
                $"{MinimumExecutionDuration} and {MaximumAllowedExecutionDuration}.");
    }
}
