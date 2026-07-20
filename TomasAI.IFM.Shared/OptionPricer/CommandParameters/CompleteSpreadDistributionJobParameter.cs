using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to complete a spread distribution job.
/// </summary>
/// <param name="EntityId">The spread distribution job entity identifier.</param>
/// <param name="JobCompleted">The date and time the job was completed.</param>
/// <param name="JobStatus">The status of the spread distribution job.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record CompleteSpreadDistributionJobParameter(
    SpreadDistributionJobEntityId EntityId,
    DateTime JobCompleted,
    SpreadDistributionJobStatus JobStatus,
    int ErrorCode)
    : ICommandParameter;
