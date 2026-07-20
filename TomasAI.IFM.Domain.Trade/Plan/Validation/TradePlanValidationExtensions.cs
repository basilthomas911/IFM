using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Plan.Validation;

public static class TradePlanValidationExtensions
{
    /// <summary>
    /// Validate trade plan
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="tradePlan"></param>
    /// <returns></returns>
    public static List<ValidationError>  ValidateTradePlan(this List<ValidationError> validationErrors, TradePlanReadModel tradePlan)
    {
        var ruleErrors = new TradePlanValidationRules().Execute(tradePlan);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
