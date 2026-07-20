using System;
using System.Collections.Generic;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Validation;

internal static class FuturesClosingPriceValidation
{
    /// <summary>
    /// Validates the FuturesDataId and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method uses the validation rules defined in <see cref="FuturesDataIdValidationRules"/> 
    /// to validate the <paramref name="futuresDataId"/>. Any errors returned by the rules are added to 
    /// the <paramref name="validationErrors"/> list.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresDataId">The <see cref="FuturesDataId"/> instance to validate.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during 
    /// validation of the <paramref name="futuresDataId"/>.</returns>
    public static List<ValidationError> ValidateFuturesDataId(this List<ValidationError> validationErrors, FuturesDataId futuresDataId)
    {
        var validator = new FuturesDataIdValidationRules();
        var ruleErrors = validator.Execute(futuresDataId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates that the closing price is within acceptable range.
    /// </summary>
    /// <param name="validationErrors">The list of validation errors to append to.</param>
    /// <param name="closingPrice">The closing price to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of validation errors.</returns>
    public static List<ValidationError> ValidateClosingPrice(this List<ValidationError> validationErrors, decimal closingPrice, string commandName)
    {
        // Validate that the closing price is non-negative
        if (closingPrice < 0)
            validationErrors.Add(new ValidationError($"{commandName}.ClosingPrice must be non-negative"));

        // Validate that the closing price is not unreasonably large (e.g., less than 1 million)
        if (closingPrice > 1_000_000m)
            validationErrors.Add(new ValidationError($"{commandName}.ClosingPrice exceeds reasonable maximum value"));

        return validationErrors;
    }
}
