using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the next seed ID for a specific seed type.
/// </summary>
/// <remarks>Use this type to specify the seed type when requesting the next seed identifier.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetNextSeedIdParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string SeedType { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetNextSeedIdParameter() { }

    [SerializationConstructor]
    public GetNextSeedIdParameter(string seedType)
    {
        SeedType = seedType ?? string.Empty;
        QueryParams = $"seedType={SeedType}";
    }

    public string Format()
        => $"{SeedType}";
}
