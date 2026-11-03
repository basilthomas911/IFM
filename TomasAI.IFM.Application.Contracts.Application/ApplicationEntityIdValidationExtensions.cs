using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Application.Shared;

public static class ApplicationEntityIdValidationExtensions
{

    extension(List<ValidationError> validationErrors)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        public List<ValidationError> ValidateApplicationEntityId(
            ApplicationEntityId? entityId,
            string commandName)
        {
            ArgumentNullException.ThrowIfNull(validationErrors);
            if (entityId is null)
                validationErrors.Add(new($"{commandName}.EntityId is null"));
            else if (entityId.ValueDate is var date && (date == DateOnly.MinValue || date == DateOnly.MaxValue))
                validationErrors.Add(new($"{commandName}.EntityId.ValueDate is invalid"));
            return validationErrors;
        }
    }
}
