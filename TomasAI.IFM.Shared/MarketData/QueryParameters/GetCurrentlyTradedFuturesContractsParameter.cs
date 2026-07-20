using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve all currently traded futures contracts.
/// </summary>
/// <remarks>Use this type when requesting all currently traded futures contracts.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetCurrentlyTradedFuturesContractsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    [SerializationConstructor]
    public GetCurrentlyTradedFuturesContractsParameter(string symbol)
    {
        Symbol = symbol;
        QueryParams = $"symbol={Symbol}";
    }

    public string Format()
        => Symbol;
}
