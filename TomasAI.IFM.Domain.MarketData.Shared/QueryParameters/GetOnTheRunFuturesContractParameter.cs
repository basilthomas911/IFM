using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the currently traded futures contract for a specific symbol.
/// </summary>
/// <remarks>Use this type to specify the symbol when requesting the currently traded futures contract.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetOnTheRunFuturesContractParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOnTheRunFuturesContractParameter() { }

    [SerializationConstructor]
    public GetOnTheRunFuturesContractParameter(string symbol)
    {
        Symbol = symbol;
        QueryParams = $"symbol={Symbol}";
    }

    public string Format()
        => Symbol;
}
