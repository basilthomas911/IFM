using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to check if a lookup type short code exists.
/// </summary>
/// <remarks>Use this type to specify the lookup type name and short code when checking for existence.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetLookupTypeShortCodeExistsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string LookupTypeName { get; init; } = string.Empty;
    [Key(1)] public string ShortCode { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLookupTypeShortCodeExistsParameter() { }

    [SerializationConstructor]
    public GetLookupTypeShortCodeExistsParameter(string lookupTypeName, string shortCode)
    {
        LookupTypeName = lookupTypeName ?? string.Empty;
        ShortCode = shortCode ?? string.Empty;
        QueryParams = $"lookupTypeName={LookupTypeName}&shortCode={ShortCode}";
    }

    public string Format()
        => $"{LookupTypeName}.{ShortCode}";
}
