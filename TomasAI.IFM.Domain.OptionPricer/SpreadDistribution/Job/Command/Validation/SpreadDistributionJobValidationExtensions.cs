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
    public static TErrors ValidateOptionTradeId<TErrors>(this TErrors validationErrors, OptionTradeEntityId optionTradeId, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(optionTradeId);
        ArgumentNullException.ThrowIfNull(validationErrors);
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
    public static TErrors ValidateSpreadDistributionJob<TErrors>(this TErrors validationErrors, SpreadDistributionJobReadModel spreadDistributionJob)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        var ruleErrors = ValidationRules.Execute(spreadDistributionJob);
        if (ruleErrors is not null)
            foreach (var validationFailure in ruleErrors)
                validationErrors.Add(validationFailure);
        return validationErrors;
    }

    /// <summary>
    /// Validates the job identifier and adds a validation error if the job ID is less than 1.
    /// </summary>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="jobId">The job identifier to validate.</param>
    /// <param name="commandName">The name of the command being validated, used to format error messages.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects.</returns>
    public static TErrors ValidateJobId<TErrors>(this TErrors validationErrors, int jobId, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
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
    public static TErrors ValidateJobCompleted<TErrors>(this TErrors validationErrors, DateTime jobCompleted, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
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
    public static TErrors ValidateJobFailed<TErrors>(this TErrors validationErrors, DateTime jobFailed, string commandName)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (jobFailed == default)
            validationErrors.Add(new($"{commandName}.JobFailed must be a valid date"));
        return validationErrors;
    }
}
