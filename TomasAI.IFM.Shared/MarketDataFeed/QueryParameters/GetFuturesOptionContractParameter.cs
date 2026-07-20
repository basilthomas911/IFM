using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a futures option contract by contract identifier.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesOptionContractParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public FuturesOptionContractReadModel? QueryForContract { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesOptionContractParameter() { }

    [SerializationConstructor]
    public GetFuturesOptionContractParameter(string contractId, FuturesOptionContractReadModel? queryForContract = null)
    {
        ContractId = contractId ?? string.Empty;
        QueryForContract = queryForContract;
        QueryParams = $"contractId={ContractId}";
    }

    public string Format()
        => ContractId;
}
