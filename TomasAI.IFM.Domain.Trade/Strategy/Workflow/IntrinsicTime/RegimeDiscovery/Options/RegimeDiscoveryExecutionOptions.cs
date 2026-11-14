using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

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

    /// <summary>Default age at which an ITI trigger ends in No Trade.</summary>
    public int TriggerMaximumAgeSeconds { get; set; } = 30;
    /// <summary>Optional age limits in seconds for individual ITI timeframes.</summary>
    public Dictionary<TimeFrameType, int> TriggerMaximumAgeSecondsByTimeFrame { get; set; } = [];

    internal int MaximumTriggerAge(TimeFrameType timeFrame) =>
        TriggerMaximumAgeSecondsByTimeFrame.GetValueOrDefault(timeFrame, TriggerMaximumAgeSeconds);

    /// <summary>Rejects missing, non-positive, or operationally unbounded timeout values at startup.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "Validates this options instance's configured properties; there is no method argument to name.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The error identifies the invalid configuration property on this self-validating options instance.")]
    public void Validate()
    {
        if (TriggerMaximumAgeSeconds <= 0 || TriggerMaximumAgeSecondsByTimeFrame.Any(entry =>
            !Enum.IsDefined(entry.Key) || entry.Key == TimeFrameType.None || entry.Value <= 0))
            throw new InvalidOperationException("Regime Discovery trigger age limits must be positive and use valid timeframes.");
        if (MaximumExecutionDuration < MinimumExecutionDuration ||
            MaximumExecutionDuration > MaximumAllowedExecutionDuration)
            throw new ArgumentOutOfRangeException(
                nameof(MaximumExecutionDuration),
                MaximumExecutionDuration,
                $"Regime Discovery maximum execution duration must be between " +
                $"{MinimumExecutionDuration} and {MaximumAllowedExecutionDuration}.");
    }
}
