using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.Validation;

public static class SpreadDistributionValidationExtensions
{
    static readonly SpreadDistributionValidationRules ValidationRules = new();
    /// <summary>
    /// validate spread distribution
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="putSpreadDistribution"></param>
    /// <param name="callSpreadDistribution"></param>
    /// <param name="commandName"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
    public static List<ValidationError> ValidateSpreadDistribution(
        this List<ValidationError> validationErrors,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (putSpreadDistribution is not null)
        {
            var ruleErrors = ValidationRules.Execute(putSpreadDistribution);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        if (callSpreadDistribution is not null)
        {
            var ruleErrors = ValidationRules.Execute(callSpreadDistribution);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        if (putSpreadDistribution is null && callSpreadDistribution is null)
            validationErrors.Add(new ValidationError($"{commandName}.PutSpreadDistribution and {commandName}.CallSpreadDistribution are empty"));
        return validationErrors;
    }
}
