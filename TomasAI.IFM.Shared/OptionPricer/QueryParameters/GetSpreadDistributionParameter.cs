using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve a spread distribution for a specific trade context.
/// </summary>
[MessagePackObject(false)]
public record GetSpreadDistributionParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }
    [Key(1)] public TradeType TradeType { get; init; }
    [Key(2)] public TradeStatus TradeStatus { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public int DaysToExpiry { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetSpreadDistributionParameter() { }

    [SerializationConstructor]
    public GetSpreadDistributionParameter(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        QueryParams = $"tradeId={TradeId}&tradeType={TradeType}&tradeStatus={TradeStatus}&valueDate={ValueDate:yyyy-MM-dd}&daysToExpiry={DaysToExpiry}";
    }

    public string Format()
        => $"{TradeId}-{TradeType}";
}
