using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve all lookup type names.
/// </summary>
/// <remarks>Use this type when requesting lookup type names.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetLookupTypeNamesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLookupTypeNamesParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "all";
}
