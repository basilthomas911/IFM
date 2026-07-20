using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve all futures contracts.
/// </summary>
/// <remarks>Use this type when requesting all futures contracts.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetFuturesContractsParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    [SerializationConstructor]
    public GetFuturesContractsParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => ActorEntityId.Default.Format();
}
