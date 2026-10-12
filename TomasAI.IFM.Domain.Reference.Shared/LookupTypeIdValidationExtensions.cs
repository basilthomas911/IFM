using MessagePack;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared;

public static class LookupTypeIdValidationExtensions
{
    static readonly LookupTypeIdValidationRules Rules = new();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The serialized collection contract retains its concrete collection shape.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retain the published collection contract used by callers and serialization.")]
    public static List<ValidationError> ValidateLookupTypeId(
        this List<ValidationError> validationErrors,
        LookupTypeId? lookupTypeId,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        validationErrors.AddRange(Rules.Execute(lookupTypeId!));
        return validationErrors;
    }
}
