using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared;

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
