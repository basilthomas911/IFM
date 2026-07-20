using System;
using System.Collections.Generic;
using System.Linq;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Validation;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Validation;

internal static class FuturesBarDataValidation
{
    /// <summary>
    /// Validates the FuturesBarDataReadModel and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method uses the validation rules defined in <see cref="FuturesBarDataReadModelValidationRules"/> 
    /// to validate the <paramref name="futuresBarData"/>. Any errors returned by the rules are added to 
    /// the <paramref name="validationErrors"/> list. If the input is null, validation is skipped and the 
    /// list is returned unchanged.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresBarData">The <see cref="FuturesBarDataReadModel"/> instance to validate. Can be null.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during 
    /// validation of the <paramref name="futuresBarData"/>.</returns>
    public static List<ValidationError> ValidateFuturesBarData(this List<ValidationError> validationErrors, FuturesBarDataReadModel futuresBarData)
    {
        var validator = new FuturesBarDataReadModelValidationRules();
        var ruleErrors = validator.Execute(futuresBarData);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the FuturesBarDataId and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method uses the validation rules defined in <see cref="FuturesBarDataIdValidationRules"/> 
    /// to validate the <paramref name="futuresBarDataId"/>. Any errors returned by the rules are added to 
    /// the <paramref name="validationErrors"/> list.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresBarDataId">The <see cref="FuturesBarDataId"/> instance to validate.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during 
    /// validation of the <paramref name="futuresBarDataId"/>.</returns>
    public static List<ValidationError> ValidateFuturesBarDataId(this List<ValidationError> validationErrors, FuturesBarDataId futuresBarDataId)
    {
        var validator = new FuturesBarDataIdValidationRules();
        var ruleErrors = validator.Execute(futuresBarDataId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates the FuturesContractV2ReadModel array and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>
    /// This method validates that the contracts array is not null or empty, and performs basic validation 
    /// on each contract's required fields. Full contract validation with reference lookups would require 
    /// IReferenceLookupService which is not available in this static context.
    /// </remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="contracts">The array of <see cref="FuturesContractV2ReadModel"/> instances to validate.</param>
    /// <param name="commandName">The name of the command for error reporting.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during 
    /// validation of the <paramref name="contracts"/>.</returns>
    public static List<ValidationError> ValidateFuturesContracts(this List<ValidationError> validationErrors, FuturesContractV2ReadModel[] contracts, string commandName)
    {
        // Validate array is not null
        if (contracts is null)
        {
            validationErrors.Add(new ValidationError($"{commandName}.Contracts is null"));
            return validationErrors;
        }

        // Validate array is not empty
        if (contracts.Length == 0)
        {
            validationErrors.Add(new ValidationError($"{commandName}.Contracts array is empty"));
            return validationErrors;
        }

        var validator = new FuturesContractValidationRules();
        for (int i = 0; i < contracts.Length; i++)
        {
            var contract = contracts[i];
            var ruleErrors = validator.Execute(contract);
            if (ruleErrors is not null)
                validationErrors.AddRange(ruleErrors);
        }
        return validationErrors;
    }

}

