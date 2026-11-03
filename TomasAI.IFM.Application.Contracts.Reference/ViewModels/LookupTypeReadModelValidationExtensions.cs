using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public static class LookupTypeReadModelValidationExtensions
{
    static readonly LookupTypeValidationRules Rules = new();

    extension(List<ValidationError> validationErrors)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The serialized collection contract retains its concrete collection shape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retain the published collection contract used by callers and serialization.")]
        public List<ValidationError> ValidateLookupType(
            LookupTypeReadModel? lookupType)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            validationErrors.AddRange(Rules.Execute(lookupType!));
            return validationErrors;
        }
    }
}
