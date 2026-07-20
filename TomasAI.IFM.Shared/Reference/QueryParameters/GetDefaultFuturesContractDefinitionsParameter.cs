using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve default futures contract definitions.
/// </summary>
/// <remarks>Use this type when requesting the default futures contract definitions.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetDefaultFuturesContractDefinitionsParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetDefaultFuturesContractDefinitionsParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "all";
}
