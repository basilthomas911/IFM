using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve existing futures option contract identifiers from a list of contract IDs.
/// </summary>
/// <remarks>
/// Use this type to specify the contract identifiers when requesting futures option contract IDs.
/// This record is typically used as a data transfer object in service or actor-based APIs.
/// </remarks>
[MessagePackObject(false)]
public record GetFuturesOptionContractIdsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string[] ContractIds { get; init; } = [];

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesOptionContractIdsParameter() { }

    [SerializationConstructor]
    public GetFuturesOptionContractIdsParameter(string[] contractIds)
    {
        ContractIds = contractIds ?? [];
        QueryParams = $"contractIds={string.Join(",", ContractIds)}";
    }

    public string Format()
        => $"contractIds.{ContractIds.Length}";
}
