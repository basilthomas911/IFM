using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Validation;

/// <summary>
/// Provides extension methods for validating lookup type view models and lookup type identifiers.
/// </summary>
/// <remarks>These extension methods add validation errors to an existing list based on the results of specific
/// validation rules. They are intended to be used as part of a validation workflow for lookup types and their
/// identifiers.</remarks>
public static class LookupTypeValidationExtensions
{
    /// <summary>
    /// validate lookup type view model
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="lookupType"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateLookupType(this List<ValidationError> validationErrors, LookupTypeReadModel lookupType)
    {
        var ruleErrors = new LookupTypeValidationRules().Execute(lookupType);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }

    /// <summary>
    /// validate lookup type id
    /// </summary>
    /// <param name="validationErrors"></param>
    /// <param name="lookupTypeId"></param>
    /// <returns></returns>
    public static List<ValidationError> ValidateLookupTypeId(this List<ValidationError> validationErrors, LookupTypeId lookupTypeId)
    {
        var ruleErrors = new LookupTypeIdValidationRules().Execute(lookupTypeId);
        if (ruleErrors is not null)
            validationErrors.AddRange(ruleErrors);
        return validationErrors;
    }
}
