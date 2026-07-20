using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to remove a lookup type.
/// </summary>
/// <param name="LookupTypeId">The unique identifier of the lookup type to remove.</param>
/// <param name="Overwrite">Indicates whether to overwrite the existing lookup type.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record RemoveLookupTypeParameter(LookupTypeId LookupTypeId, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
