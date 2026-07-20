using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.CommandParameters;

/// <summary>
/// Represents the parameters required to submit a spread distribution job.
/// </summary>
/// <param name="SpreadDistributionJob">The spread distribution job to submit.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record SubmitSpreadDistributionJobParameter(
    SpreadDistributionJobReadModel SpreadDistributionJob,
    int ErrorCode)
    : ICommandParameter;
