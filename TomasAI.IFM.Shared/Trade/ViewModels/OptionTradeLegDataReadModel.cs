using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model containing calculated and market data for a single option leg on a given value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members and
/// non-MessagePack types are excluded via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeLegDataReadModel
{
    /// <summary>Parent order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Value (trading) date for this snapshot.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Option leg contract identifier.</summary>
    [Key(3)]
    public string OptionLegId { get; init; } = string.Empty;

    /// <summary>Trade strategy/type.</summary>
    [Key(4)]
    public TradeType TradeType { get; init; }

    /// <summary>Remaining days to contract expiry.</summary>
    [Key(5)]
    public int DaysToExpiry { get; init; }

    /// <summary>Trade lifecycle status at this snapshot.</summary>
    [Key(6)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Best bid price for the option.</summary>
    [Key(7)]
    public decimal BidPrice { get; init; }

    /// <summary>Best ask price for the option.</summary>
    [Key(8)]
    public decimal AskPrice { get; init; }

    /// <summary>Implied volatility estimate.</summary>
    [Key(9)]
    public double ImpliedVolatility { get; init; }

    /// <summary>Option delta.</summary>
    [Key(10)]
    public double Delta { get; init; }

    /// <summary>Option gamma.</summary>
    [Key(11)]
    public double Gamma { get; init; }

    /// <summary>Option theta.</summary>
    [Key(12)]
    public double Theta { get; init; }

    /// <summary>Option vega.</summary>
    [Key(13)]
    public double Vega { get; init; }

    /// <summary>Option rho.</summary>
    [Key(14)]
    public double Rho { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(15)]
    public DateTime CreatedOn { get; init; }

    /// <summary>Record creator.</summary>
    [Key(16)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last update timestamp (UTC preferred).</summary>
    [Key(17)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>Last updater.</summary>
    [Key(18)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Attached leg details (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public OptionTradeLegReadModel? OptionLeg { get; private set; }

    /// <summary>Derived identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionLegDataId Id => new(OrderId, TradeId, ValueDate, TradeType, DaysToExpiry, TradeStatus, OptionLegId);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && !string.IsNullOrEmpty(OptionLegId);

    /// <summary>Computed notional asset value (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public decimal AssetValue => OptionLeg!.Quantity * OptionLeg.StrikePrice * 50;

    /// <summary>Computed mid price (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public decimal OptionPrice => (BidPrice + AskPrice) / 2;

    /// <summary>Parameterless constructor for serializers.</summary>
    public OptionTradeLegDataReadModel() { }

    /// <summary>Creates a new option leg data snapshot.</summary>
    public OptionTradeLegDataReadModel(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        string optionLegId,
        TradeType tradeType,
        int daysToExpiry,
        TradeStatus tradeStatus,
        decimal bidPrice,
        decimal askPrice,
        double impliedVolatility,
        double delta,
        double gamma,
        double theta,
        double vega,
        double rho,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        OptionLegId = optionLegId ?? string.Empty;
        TradeType = tradeType;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        ImpliedVolatility = impliedVolatility;
        Delta = delta;
        Gamma = gamma;
        Theta = theta;
        Vega = vega;
        Rho = rho;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>Factory for a default snapshot with zeroed metrics and empty identifiers.</summary>
    public static OptionTradeLegDataReadModel Default(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => new(
            orderId: orderId,
            tradeId: tradeId,
            valueDate: valueDate,
            optionLegId: string.Empty,
            tradeType: tradeType,
            daysToExpiry: daysToExpiry,
            tradeStatus: tradeStatus,
            bidPrice: 0,
            askPrice: 0,
            impliedVolatility: 0,
            delta: 0,
            gamma: 0,
            theta: 0,
            vega: 0,
            rho: 0,
            createdOn: DateTime.UtcNow,
            createdBy: string.Empty,
            updatedOn: DateTime.UtcNow,
            updatedBy: string.Empty);

    /// <summary>Creates a deep copy including the attached OptionLeg if present.</summary>
    public OptionTradeLegDataReadModel Copy()
        => new OptionTradeLegDataReadModel(
            orderId: OrderId,
            tradeId: TradeId,
            valueDate: ValueDate,
            optionLegId: OptionLegId,
            tradeType: TradeType,
            daysToExpiry: DaysToExpiry,
            tradeStatus: TradeStatus,
            bidPrice: BidPrice,
            askPrice: AskPrice,
            impliedVolatility: ImpliedVolatility,
            delta: Delta,
            gamma: Gamma,
            theta: Theta,
            vega: Vega,
            rho: Rho,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy).SetOptionLeg(OptionLeg?.Copy());

    /// <summary>Attaches an OptionLeg and updates the OptionLegId accordingly.</summary>
    public OptionTradeLegDataReadModel SetOptionLeg(OptionTradeLegReadModel? optionLeg)
    {
        OptionLeg = optionLeg!;
        return this with { OptionLegId = OptionLeg.ContractId };
    }
}