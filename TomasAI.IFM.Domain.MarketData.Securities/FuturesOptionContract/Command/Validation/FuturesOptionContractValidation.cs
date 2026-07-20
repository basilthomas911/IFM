using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Validation;

internal static class FuturesOptionContractValidation
{
    /// <summary>
    /// Validates a futures option contract and appends any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method uses predefined validation rules to check the provided futures option contract
    /// for errors. If validation rules produce errors, they are added to the <paramref name="validationErrors"/> list.
    /// The method does not clear or modify existing entries in the list.</remarks>
    /// <param name="validationErrors">The list to which validation errors will be added. This list must not be null.</param>
    /// <param name="futuresOptionContract">The futures option contract to validate. This parameter must not be null.</param>
    /// <param name="refLookupService">The reference lookup service used to retrieve validation rules and reference data. This parameter must not be
    /// null.</param>
    /// <returns>The original list of validation errors, with any new errors from the validation process appended.</returns>
    public static List<ValidationError> ValidateFuturesOptionContract(this List<ValidationError> validationErrors, FuturesOptionContractReadModel futuresOptionContract, IReferenceLookupService refLookupService)
    {
        var ruleErrors = new FuturesOptionContractReadModelValidationRules(refLookupService).Execute(futuresOptionContract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// Validates a collection of futures option contracts and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method applies a set of validation rules to each contract in the <paramref
    /// name="contracts"/> array. If the array is null or empty, a general error is added to the <paramref
    /// name="validationErrors"/> list. Otherwise, validation errors specific to each contract are added.</remarks>
    /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
    /// <param name="contracts">An array of futures option contracts to validate. If the array is null or empty, a general validation error is added.</param>
    /// <param name="refLookupService">A service used to retrieve reference data required for validation rules.</param>
    /// <returns>The updated list of validation errors, including any errors found during the validation of the provided
    /// contracts.</returns>
    public static List<ValidationError> ValidateFuturesOptionContracts(this List<ValidationError> validationErrors, FuturesOptionContractReadModel[] contracts, IReferenceLookupService refLookupService)
    {
        if (contracts is null || contracts.Length == 0)
        {
            validationErrors.Add(new ValidationError("Futures option contracts parameter is null or empty"));
        }
        else
        {
            var validationRules = new FuturesOptionContractReadModelValidationRules(refLookupService);
            foreach (var contract in contracts)
            {
                var ruleErrors = validationRules.Execute(contract);
                if (ruleErrors is not null && ruleErrors.Length > 0)
                    validationErrors.AddRange(ruleErrors);
            }
        }
        return validationErrors;
    }

}
