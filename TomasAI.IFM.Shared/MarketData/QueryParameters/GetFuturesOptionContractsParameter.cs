using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve futures option contracts for a specific symbol.
/// </summary>
/// <remarks>Use this type to specify the underlying futures symbol when requesting futures option contracts.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetFuturesOptionContractsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public string Symbol { get; init; } = string.Empty;

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFuturesOptionContractsParameter() { }

    [SerializationConstructor]
    public GetFuturesOptionContractsParameter(string symbol)
    {
        Symbol = symbol ?? string.Empty;
        QueryParams = $"symbol={Symbol}";
    }

    public string Format()
        => Symbol;
}
