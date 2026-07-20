using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to change a lookup type.
/// </summary>
/// <param name="LookupTypeId">The unique identifier of the lookup type to change.</param>
/// <param name="LookupType">The updated lookup type details. Cannot be null.</param>
/// <param name="Overwrite">Indicates whether to overwrite the existing lookup type.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record ChangeLookupTypeParameter(LookupTypeId LookupTypeId, LookupTypeReadModel LookupType, bool Overwrite, int ErrorCode)
    : ICommandParameter
{
}
