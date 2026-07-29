using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.Validation;

public static class SpreadDistributionValidationExtensions
{
    /// <summary>
    /// validate spread distribution
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="putSpreadDistribution"></param>
    /// <param name="callSpreadDistribution"></param>
    /// <param name="commandName"></param>
    public static List<ValidationError> ValidateSpreadDistribution(
        this List<ValidationError> validationErrors,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution,
        string commandName)
    {
        var validationRules = new SpreadDistributionValidationRules();
        if (putSpreadDistribution is not null)
        {
            var ruleErrors = validationRules.Execute(putSpreadDistribution);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        if (callSpreadDistribution is not null)
        {
            var ruleErrors = validationRules.Execute(callSpreadDistribution);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        if (putSpreadDistribution is null && callSpreadDistribution is null)
            validationErrors.Add(new ValidationError($"{commandName}.PutSpreadDistribution and {commandName}.CallSpreadDistribution are empty"));
        return validationErrors;
    }
}
