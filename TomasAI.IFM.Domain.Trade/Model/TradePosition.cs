using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradePosition(
    int orderId,
    int tradeId,
    TradeType tradeType,
    DateOnly valueDate,
    int daysToExpiry,
    TradeStatus tradeStatus,
    decimal commission,
    int deltaHedge,
    decimal netSpread,
    decimal tradeValue,
    decimal tradePnl,
    decimal assetPrice,
    double otmProbability,
    decimal forwardPrice,
    double forwardLossRatio,
    double lossProbability,
    double riskFreeRate,
    DateTime createdOn,
    string createdBy,
    DateTime updatedOn,
    string updatedBy) :  ITradePosition
{
    public TradePositionEntityId Id => new(OrderId, TradeId, ValueDate, TradeType, TradeStatus, DaysToExpiry);
    public int OrderId { get; private set; } = orderId;
    public int TradeId { get; private set; } = tradeId;
    public TradeType TradeType { get; private set; } = tradeType;
    public DateOnly ValueDate { get; private set; } = valueDate;
    public int DaysToExpiry { get; private set; } = daysToExpiry;
    public TradeStatus TradeStatus { get; private set; } = tradeStatus;
    public decimal Commission { get; private set; } = commission;
    public int DeltaHedge { get; private set; } = deltaHedge;
    public decimal NetSpread => netSpread;
    public decimal TradeValue => tradeValue;
    public decimal TradePnl { get; private set; } = tradePnl;
    public decimal AssetPrice { get; private set; } = assetPrice;
    public double OTMProbability { get; private set; } = otmProbability;
    public decimal ForwardPrice { get; private set; } = forwardPrice;
    public double ForwardLossRatio { get; private set; } = forwardLossRatio;
    public double LossProbability { get; private set; } = lossProbability;
    public double RiskFreeRate { get; private set; } = riskFreeRate;
    public DateTime CreatedOn { get; private set; } = createdOn;
    public string CreatedBy { get; private set; } = createdBy;
    public DateTime UpdatedOn { get; private set; } = updatedOn;
    public string UpdatedBy { get; private set; } = updatedBy;
    public IOptionLegDataCollection OptionLegData { get; private set; } = new OptionLegDataCollection(
            tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, assetPrice);

    public TradePosition(
       int orderId,
       int tradeId,
       TradeType tradeType,
       DateOnly valueDate,
       int daysToExpiry,
       TradeStatus tradeStatus,
       DateTime createdOn,
       string createdBy) : this(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus,
           0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, createdOn, createdBy, createdOn, createdBy)
    {
    }

    public TradePosition(TradePositionReadModel e, DateTime createdOn, string createdBy) :this(
        orderId: e.EntityId.OrderId,
        tradeId: e.EntityId.TradeId,
        tradeType: e.EntityId.TradeType,
        valueDate: e.EntityId.ValueDate,
        daysToExpiry: e.EntityId.DaysToExpiry,
        tradeStatus: e.EntityId.TradeStatus,
        commission: e.Commission,
        deltaHedge: e.DeltaHedge,
        netSpread: e.NetSpread,
        tradeValue: e.TradeValue,
        tradePnl: e.TradePnl,
        assetPrice: e.AssetPrice,
        otmProbability: e.OTMProbability,
        forwardPrice: e.ForwardPrice,
        forwardLossRatio: e.ForwardLossRatio,
        lossProbability: e.LossProbability,
        riskFreeRate: e.RiskFreeRate,
        createdOn: createdOn,
        createdBy: createdBy,
        updatedOn: createdOn,
        updatedBy: createdBy)
    {
        foreach (var o in e.OptionLegData)
            OptionLegData.Add(o.ToOptionLegData(e.EntityId, createdOn, createdBy, createdOn, createdBy));
    }
   
    /// <summary>
    /// add option leg data
    /// </summary>
    /// <param name="optionLegData"></param>
    public ITradePosition AddOptionLegData(ICollection<IOptionLegData> optionLegData)
    {
        if (optionLegData is null)
            throw new ArgumentNullException("optionLegData", $"SpreadTradeData.AddOptionLegData: optionLegData argument is null");
        foreach (var e in optionLegData)
        {
            if (OptionLegData.Exists(e.OptionLegId))
                OptionLegData.Remove(e.OptionLegId);
            OptionLegData.Add(e);
        }
        return this;
    }

    /// <summary>
    /// set trade status
    /// </summary>
    /// <param name="tradeStatus"></param>
    /// <returns></returns>
    public ITradePosition SetTradeStatus(TradeStatus tradeStatus)
    {
        TradeStatus = tradeStatus;
        return this;
    }

    /// <summary>
    /// set trade pnl
    /// </summary>
    /// <param name="tradePnl"></param>
    /// <returns></returns>
    public ITradePosition SetTradePnl(decimal tradePnl)
    {
        TradePnl = tradePnl;
        return this;
    }

    /// <summary>
    /// set trade commission
    /// </summary>
    /// <param name="commission"></param>
    /// <returns></returns>
    public ITradePosition SetCommission(decimal commission)
    {
        Commission = commission;
        return this;
    }

    /// <summary>
    /// set asset price
    /// </summary>
    /// <param name="assetPrice"></param>
    /// <returns></returns>
    public ITradePosition SetAssetPrice(decimal assetPrice)
    {
        AssetPrice = assetPrice;
        return this;
    }

    /// <summary>
    /// set no touch probability
    /// </summary>
    /// <param name="noTouchProbability"></param>
    /// <returns></returns>
    public ITradePosition SetOTMProbability(double otmProbability)
    {
        OTMProbability = otmProbability;
        return this;
    }

    public ITradePosition SetForwardPrice(decimal maxPrice)
    {
        ForwardPrice = maxPrice;
        return this;
    }

    public ITradePosition SetForwardLossRatio(double forwardLossRatio)
    {

        ForwardLossRatio = forwardLossRatio;
        return this;
    }

    public ITradePosition SetLossProbability(double lossProbability)
    {

        LossProbability = lossProbability;
        return this;
    }

    public ITradePosition SetRiskFreeRate(double riskFreeRate)
    {
        RiskFreeRate = riskFreeRate;
        return this;
    }

    public ITradePosition SetUpdated(DateTime updatedOn, string updatedBy)
    {
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
        return this;
    }

    public ITradePosition ReplaceOptionLegData(IOptionLegData optionLegData)
    {
        OptionLegData.Remove(optionLegData.OptionLegId);
        OptionLegData.Add(optionLegData);
        return this;
    }

    public ITradePosition SetEndOfDayStatus()
    {
        TradeStatus = TradeStatus.EndOfDay;
        return this;
    }

    public TradePositionReadModel ToViewModel()
        => new TradePositionReadModel (
            orderId: Id.OrderId,
            tradeId: Id.TradeId,
            tradeType: Id.TradeType,
            valueDate: ValueDate,
            daysToExpiry: DaysToExpiry,
            tradeStatus: TradeStatus,
            commission: Commission,
            deltaHedge: DeltaHedge,
            netSpread: NetSpread,
            tradeValue: TradeValue,
            tradePnl: TradePnl,
            assetPrice: AssetPrice,
            otmProbability: OTMProbability,
            forwardPrice: ForwardPrice,
            forwardLossRatio: ForwardLossRatio,
            lossProbability: LossProbability,
            riskFreeRate: RiskFreeRate,
            createdOn: CreatedOn,
            createdBy: CreatedBy,
            updatedOn: UpdatedOn,
            updatedBy: UpdatedBy
       ).AddOptionLegData(OptionLegData.Select(e => e.ToDataModel()).ToList());
}

public static class SpreadTradeDataViewModelExtension
{
    public static ITradePosition ToSpreadTradeData(this TradePositionReadModel e, DateTime createdOn, string createdBy)
        => new TradePosition(
            orderId: e.EntityId.OrderId,
            tradeId: e.EntityId.TradeId,
            tradeType: e.EntityId.TradeType,
            valueDate: e.EntityId.ValueDate,
            daysToExpiry: e.EntityId.DaysToExpiry,
            tradeStatus: e.EntityId.TradeStatus,
            createdOn: createdOn,
            createdBy: createdBy)
                .SetCommission(e.Commission)
                .SetAssetPrice(e.AssetPrice)
                .SetOTMProbability(e.OTMProbability)
                .SetForwardPrice(e.ForwardPrice)
                .SetForwardLossRatio(e.ForwardLossRatio)
                .SetLossProbability(e.LossProbability)
                .SetRiskFreeRate(e.RiskFreeRate)
            .AddOptionLegData(e.OptionLegData
                    .Select(o => o.ToOptionLegData(e.EntityId, createdOn, createdBy, createdOn, createdBy)).ToArray());
}
