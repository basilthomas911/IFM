using System.Collections.Generic;
using System.Diagnostics.Contracts;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.Validation;

public static class MarketDataValidationExtensions
{
    public const string ContractsErrorMessage = "contracts parameter is null or empty";
    public const string EntityIdErrorMessage = "entity id is null or empty";

    /// <summary>
    /// Validates a collection of futures contracts and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method applies a set of validation rules to each contract in the <paramref
    /// name="contracts"/> array. If the array is null or empty, a general error is added to the <paramref
    /// name="validationErrors"/> list. Otherwise, validation errors specific to each contract are added.</remarks>
    /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
    /// <param name="contracts">An array of futures contracts to validate. If the array is null or empty, a general validation error is added.</param>
    /// <param name="refLookupService">A service used to retrieve reference data required for validation rules.</param>
    /// <returns>The updated list of validation errors, including any errors found during the validation of the provided
    /// contracts.</returns>
    public static List<ValidationError> ValidateFuturesContracts(this List<ValidationError> validationErrors, FuturesContractV2ReadModel[] contracts, IReferenceLookupService refLookupService)
    {
        if (contracts is null || contracts.Length == 0)
        {
            validationErrors.Add(new ValidationError(ContractsErrorMessage));
        }
        else
        {
            var validationRules = new FuturesContractValidationRules();
            foreach (var contract in contracts)
            {
                var ruleErrors = validationRules.Execute(contract);
                if (ruleErrors is not null && ruleErrors.Length > 0)
                    validationErrors.AddRange(ruleErrors);
            }
        }
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified futures contract and appends any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method uses a set of predefined validation rules to check the integrity and correctness
    /// of the specified futures contract. Any validation errors encountered are added to the provided <paramref
    /// name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">A list to which validation errors will be added. This list must not be null.</param>
    /// <param name="contract">The futures contract to validate. This parameter must not be null.</param>
    /// <param name="refLookupService">A reference lookup service used to retrieve validation rules and reference data. This parameter must not be
    /// null.</param>
    /// <returns>The original list of validation errors, with any additional errors from the validation process appended.</returns>
    public static List<ValidationError> ValidateFuturesContract(this List<ValidationError> validationErrors, FuturesContractV2ReadModel contract, IReferenceLookupService refLookupService)
    {
        var ruleErrors = new FuturesContractValidationRules().Execute(contract);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }



    /// <summary>
    /// Validates the specified entity ID and adds a validation error to the provided list if the entity ID is null or
    /// empty.
    /// </summary>
    /// <remarks>This method checks whether the <paramref name="entityId"/> is null or empty. If the
    /// validation fails, a predefined error message is added to the <paramref name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">The list of validation errors to which any detected errors will be added.</param>
    /// <param name="entityId">The entity ID to validate. Must not be null or empty.</param>
    /// <returns>The updated list of validation errors, including any errors related to the entity ID validation.</returns>
    public static List<ValidationError> ValidateEntityId(this List<ValidationError> validationErrors, FuturesOptionContractId entityId)
    {
        if (entityId is null && entityId!.IsEmpty)
            validationErrors.Add(new ValidationError(EntityIdErrorMessage));
        return validationErrors;
    }

    /// <summary>
    /// Validates the specified <see cref="FuturesContractIdParser"/> against a set of predefined rules and appends any
    /// validation errors to the provided list of <see cref="ValidationError"/> objects.
    /// </summary>
    /// <remarks>This method uses a set of predefined validation rules to check the validity of the provided
    /// <see cref="FuturesContractIdParser"/>. If any validation errors are found, they are appended to the <paramref
    /// name="validationErrors"/> list. The method ensures that the original list is returned with any additional errors
    /// included.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added. This parameter
    /// cannot be null.</param>
    /// <param name="futuresContractId">The <see cref="FuturesContractIdParser"/> to validate. This parameter cannot be null.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any errors found during validation.</returns>
    public static List<ValidationError> ValidateFuturesContractEntityId(this List<ValidationError> validationErrors, FuturesContractId futuresContractId)
    {
        var ruleErrors = new FuturesContractEntityIdValidationRules().Execute(futuresContractId);
        if (ruleErrors is not null && ruleErrors.Length > 0)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

}
