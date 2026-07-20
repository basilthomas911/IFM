using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.OptionPricer.QueryParameters;

/// <summary>
/// Represents the parameters required to check if a spread distribution job is in progress.
/// </summary>
[MessagePackObject(false)]
public record GetSpreadDistributionJobInProgressParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetSpreadDistributionJobInProgressParameter() { }

    [SerializationConstructor]
    public GetSpreadDistributionJobInProgressParameter(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}";
    }

    public string Format()
        => $"{OrderId}-{TradeId}";
}
