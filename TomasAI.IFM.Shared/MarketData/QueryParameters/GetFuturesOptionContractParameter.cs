using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a specific futures option contract by its contract identifier.
/// </summary>
/// <remarks>
/// Use this type to specify the contract identifier when requesting a futures option contract.
/// This record is typically used as a data transfer object in service or actor-based APIs.
/// </remarks>
[MessagePackObject(false)]
public record GetFuturesOptionContractParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesOptionContractParameter() { }

    [SerializationConstructor]
    public GetFuturesOptionContractParameter(string contractId)
    {
        ContractId = contractId ?? string.Empty;
        QueryParams = $"contractId={ContractId}";
    }

    public string Format()
        => $"{ContractId}";
}
