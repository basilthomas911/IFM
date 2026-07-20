using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// Specifies the possible statuses of a spread distribution job within the processing workflow.
/// </summary>
/// <remarks>Use this enumeration to track and manage the state transitions of a spread distribution job, such as
/// when monitoring progress, handling errors, or performing cleanup operations. Each status value represents a distinct
/// phase in the job's lifecycle.</remarks>
public enum SpreadDistributionJobStatus
{
    InProgress,
    Completed,
    Failed,
    Cleared
}

public static class SpreadDistributionJobStatusExtensions
{
    public static string ToStringFast(this SpreadDistributionJobStatus value) => value switch
    {
        SpreadDistributionJobStatus.InProgress => nameof(SpreadDistributionJobStatus.InProgress),
        SpreadDistributionJobStatus.Completed => nameof(SpreadDistributionJobStatus.Completed),
        SpreadDistributionJobStatus.Failed => nameof(SpreadDistributionJobStatus.Failed),
        SpreadDistributionJobStatus.Cleared => nameof(SpreadDistributionJobStatus.Cleared),
        _ => value.ToString()
    };
}
