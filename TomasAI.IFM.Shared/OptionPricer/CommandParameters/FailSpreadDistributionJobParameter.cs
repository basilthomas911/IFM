using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to fail a spread distribution job.
/// </summary>
/// <param name="EntityId">The spread distribution job entity identifier.</param>
/// <param name="JobFailed">The date and time the job failed.</param>
/// <param name="JobStatus">The status of the spread distribution job.</param>
/// <param name="ErrorMessage">The error message describing the failure.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record FailSpreadDistributionJobParameter(
    SpreadDistributionJobEntityId EntityId,
    DateTime JobFailed,
    SpreadDistributionJobStatus JobStatus,
    string ErrorMessage,
    int ErrorCode)
    : ICommandParameter;
