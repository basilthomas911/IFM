using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Validation;

/// <summary>
/// Extension methods for validating spread distribution job commands.
/// </summary>
public static class SpreadDistributionJobValidationExtensions
{
    /// <summary>Validates the option-trade identity used by a spread distribution job.</summary>
    public static List<ValidationError> ValidateOptionTradeId(this List<ValidationError> validationErrors, OptionTradeEntityId optionTradeId, string commandName)
    {
        if (optionTradeId.OrderId < 1)
            validationErrors.Add(new($"{commandName}.OrderId must be > 0"));
        if (optionTradeId.TradeId < 1)
            validationErrors.Add(new($"{commandName}.TradeId must be > 0"));
        return validationErrors;
    }

    static readonly SpreadDistributionJobValidationRules ValidationRules = new();
    /// <summary>
    /// Validates the spread distribution job read model and adds any validation errors to the provided list.
    /// </summary>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="spreadDistributionJob">The spread distribution job to validate.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects.</returns>
    public static List<ValidationError> ValidateSpreadDistributionJob(this List<ValidationError> validationErrors, SpreadDistributionJobReadModel spreadDistributionJob)
    {
        var ruleErrors = ValidationRules.Execute(spreadDistributionJob);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the job identifier and adds a validation error if the job ID is less than 1.
    /// </summary>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="jobId">The job identifier to validate.</param>
    /// <param name="commandName">The name of the command being validated, used to format error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects.</returns>
    public static List<ValidationError> ValidateJobId(this List<ValidationError> validationErrors, int jobId, string commandName)
    {
        if (jobId < 1)
            validationErrors.Add(new($"{commandName}.JobId must be > 0"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the job completed timestamp and adds a validation error if the timestamp is the default value.
    /// </summary>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="jobCompleted">The job completion timestamp to validate.</param>
    /// <param name="commandName">The name of the command being validated, used to format error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects.</returns>
    public static List<ValidationError> ValidateJobCompleted(this List<ValidationError> validationErrors, DateTime jobCompleted, string commandName)
    {
        if (jobCompleted == default)
            validationErrors.Add(new($"{commandName}.JobCompleted must be a valid date"));
        return validationErrors;
    }

    /// <summary>
    /// Validates the job failed timestamp and adds a validation error if the timestamp is the default value.
    /// </summary>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="jobFailed">The job failure timestamp to validate.</param>
    /// <param name="commandName">The name of the command being validated, used to format error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects.</returns>
    public static List<ValidationError> ValidateJobFailed(this List<ValidationError> validationErrors, DateTime jobFailed, string commandName)
    {
        if (jobFailed == default)
            validationErrors.Add(new($"{commandName}.JobFailed must be a valid date"));
        return validationErrors;
    }
}
