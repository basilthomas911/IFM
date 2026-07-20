using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to delete spread distribution jobs in progress.
/// </summary>
/// <param name="EntityId">The spread distribution job entity identifier.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record DeleteSpreadDistributionJobsInProgressParameter(
    SpreadDistributionJobEntityId EntityId,
    int ErrorCode)
    : ICommandParameter;
