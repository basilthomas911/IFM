using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Command.Validation;

/// <summary>
/// Provides extension methods for validating FundReadModel instances and collecting validation errors.
/// </summary>
public static class FundValidationExtensions
{
    /// <summary>
    /// Validates the FundReadModel against the FundValidationRules and adds any validation errors to the provided list of validation errors.
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="fund"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateFund(this List<ValidationError> validationErrors, FundReadModel fund)
    {
        var ruleErrors = new FundValidationRules().Execute(fund);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified fund order and adds any validation errors to the provided collection.
    /// </summary>
    /// <param name="validationErrors">The collection to which any validation errors found will be added. Must not be null.</param>
    /// <param name="fundOrder">The fund order to validate. Must not be null.</param>
    /// <returns>The updated list of validation errors, including any errors found during validation of the fund order.</returns>
    public static List<ValidationError> ValidateFundOrder(this List<ValidationError> validationErrors, FundOrderReadModel fundOrder)
    {
        var ruleErrors = new FundOrderValidationRules().Execute(fundOrder);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified fund order trade and adds any validation errors to the provided collection.
    /// </summary>
    /// <param name="validationErrors">The list to which any validation errors found during validation will be added. Cannot be null.</param>
    /// <param name="fundOrderTrade">The fund order trade to validate. Cannot be null.</param>
    /// <returns>The list of validation errors, including any errors found during validation of the specified fund order trade.</returns>
    public static List<ValidationError> ValidateFundOrderTrade(this List<ValidationError> validationErrors, FundOrderTradeReadModel fundOrderTrade)
    {
        var ruleErrors = new FundOrderTradeValidationRules().Execute(fundOrderTrade);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
