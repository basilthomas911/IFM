using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.CommandParameters;

/// <summary>
/// Represents the parameters required to add a lookup type.
/// </summary>
/// <param name="LookupType">The lookup type details to add. Cannot be null.</param>
/// <param name="ErrorCode">The error code associated with the operation. Used to indicate specific error conditions or statuses.</param>
public record AddLookupTypeParameter(LookupTypeReadModel LookupType, int ErrorCode)
    : ICommandParameter
{
}
