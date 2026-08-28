using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Application.Shared;

[MessagePackObject(AllowPrivate = true)]
public record ApplicationEntityId(
    [property: Key(0)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; defaults to current UTC year.
    /// </summary>
    public ApplicationEntityId() : this(DateOnly.FromDateTime(DateTime.UtcNow)) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    public string Format() 
        => ValueDate.ToString("yyyy-MM-dd");
}

public static class ApplicationEntityIdValidationExtensions
{
    public static List<ValidationError> ValidateApplicationEntityId(
        this List<ValidationError> validationErrors,
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
