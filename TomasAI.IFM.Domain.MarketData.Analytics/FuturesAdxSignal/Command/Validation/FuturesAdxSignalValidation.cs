using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Validation;

public static class FuturesAdxSignalValidation
{
    /// <summary>
    /// Validates the specified futures MACD signal identifier and adds any validation errors to the provided
    /// collection.
    /// </summary>
    /// <remarks>This method extends a list of validation errors by validating a futures MACD signal
    /// identifier and appending any new errors. The original list is modified in place and also returned for
    /// convenience.</remarks>
    /// <param name="validationErrors">The list to which any validation errors found during validation will be added. Cannot be null.</param>
    /// <param name="futuresMacdSignalId">The futures MACD signal identifier to validate.</param>
    /// <returns>The list of validation errors, including any errors found during validation of the specified signal identifier.</returns>
    public static List<ValidationError> ValidateFuturesMacdSignalId(this List<ValidationError> validationErrors, FuturesMacdSignalId futuresMacdSignalId)
    {
        var ruleErrors = new FuturesMacdSignalIdValidationRules().Execute(futuresMacdSignalId);
        if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
