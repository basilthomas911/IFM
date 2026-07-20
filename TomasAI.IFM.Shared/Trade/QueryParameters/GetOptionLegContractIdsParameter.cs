using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve option leg contract ids for a specific trade.
/// </summary>
[MessagePackObject(false)]
public record GetOptionLegContractIdsParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOptionLegContractIdsParameter() { }

    [SerializationConstructor]
    public GetOptionLegContractIdsParameter(int tradeId)
    {
        TradeId = tradeId;
        QueryParams = $"tradeId={TradeId}";
    }

    public string Format()
        => $"{TradeId}";
}
