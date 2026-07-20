using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the symbol for a futures contract by its contract identifier.
/// </summary>
[MessagePackObject(false)]
public record GetFuturesContractSymbolParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesContractSymbolParameter() { }

    [SerializationConstructor]
    public GetFuturesContractSymbolParameter(string contractId)
    {
        ContractId = contractId ?? string.Empty;
        QueryParams = $"contractId={ContractId}";
    }

    public string Format()
        => ContractId;
}
