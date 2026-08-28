using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Validation;

public static class LookupTypeCommandValidationExtensions
{
    public static List<ValidationError> ValidateLookupTypeIdentityMatches(
        this List<ValidationError> validationErrors,
        LookupTypeId? entityId,
        LookupTypeId? payloadId,
        string payloadName,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (IsValid(entityId) && IsValid(payloadId) && entityId != payloadId)
            validationErrors.Add(new($"{commandName}.EntityId must match {payloadName}"));
        return validationErrors;
    }

    static bool IsValid(LookupTypeId? id)
        => id is not null && !string.IsNullOrWhiteSpace(id.LookupTypeName) && id.OrderId >= 0;
}
