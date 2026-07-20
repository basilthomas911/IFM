using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve lookup type short codes for a specific lookup type.
/// </summary>
/// <remarks>Use this type to specify the lookup type name when requesting short codes.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetLookupTypeShortCodesParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string LookupTypeName { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLookupTypeShortCodesParameter() { }

    [SerializationConstructor]
    public GetLookupTypeShortCodesParameter(string lookupTypeName)
    {
        LookupTypeName = lookupTypeName ?? string.Empty;
        QueryParams = $"lookupTypeName={LookupTypeName}";
    }

    public string Format()
        => $"{LookupTypeName}";
}
