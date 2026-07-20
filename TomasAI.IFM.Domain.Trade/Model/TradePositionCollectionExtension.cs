using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>
/// 
/// </summary>
public static class TradePositionCollectionExtension
{

    /// <summary>
    ///  get added event
    /// </summary>
    /// <param name="tradePositions"></param>
    /// <param name="tradeType"></param>
    /// <param name="commandId"></param>
    /// <param name="key"></param>
    /// <param name="assetPrice"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static TradePositionAddedEvent GetAddedEvent(this ITradePositionCollection tradePositions, TradePositionEntityId key, TradeType tradeType, decimal assetPrice, double riskFreeRate, DateTime updatedOn, string updatedBy)
        => tradeType switch {
            TradeType.ShortIronCondor or TradeType.LongIronCondor => CreateIronCondorTradePositionAddedEvent( tradePositions, key, assetPrice, riskFreeRate, updatedOn, updatedBy),
            _ => throw new NotImplementedException()
        };

    /// <summary>
    /// get updated event
    /// </summary>
    /// <param name="tradePositions"></param>
    /// <param name="baseTradeType"></param>
    /// <param name="key"></param>
    /// <param name="optionLegId"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    public static TradePositionUpdatedEvent GetUpdatedEvent(this ITradePositionCollection tradePositions, TradePositionEntityId key, TradeType baseTradeType,  string optionLegId, DateTime updatedOn, string updatedBy)
        => new()
        {
            OptionLegId = optionLegId,
            PutTradePosition = tradePositions.IntraDay(baseTradeType == TradeType.ShortIronCondor ? TradeType.PutCreditSpread : TradeType.PutDebitSpread, key.ValueDate)?.ToViewModel(),
            CallTradePosition = tradePositions.IntraDay(baseTradeType == TradeType.ShortIronCondor ? TradeType.CallCreditSpread : TradeType.CallDebitSpread, key.ValueDate)?.ToViewModel(),
            TradePositionChangeSource = tradePositions.Source(key.TradeType),
            UpdatedOn = updatedOn,
            UpdatedBy = updatedBy
        };

    /// <summary>
    ///  create iron condor trade position added event
    /// </summary>
    /// <param name="commandId"></param>
    /// <param name="tradePositions"></param>
    /// <param name="key"></param>
    /// <param name="assetPrice"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    static TradePositionAddedEvent CreateIronCondorTradePositionAddedEvent(  ITradePositionCollection tradePositions, TradePositionEntityId key, decimal assetPrice, double riskFreeRate, DateTime updatedOn, string updatedBy)
    {
        var tradePosition = key.TradeType switch
        {
            TradeType.PutCreditSpread or
            TradeType.CallCreditSpread or
            TradeType.PutDebitSpread or
            TradeType.CallDebitSpread  => GetIronCondorTradePosition(key.TradeType),
            _ => throw new NotImplementedException()
        };
        return new TradePositionAddedEvent
        {
            OrderId = key.OrderId,
            AssetPrice = assetPrice,
            RiskFreeRate = riskFreeRate,
            TradePosition = tradePosition,
            CreatedOn = updatedOn,
            CreatedBy = updatedBy
        };

        TradePositionReadModel GetIronCondorTradePosition(TradeType tradeType)
        {
            var id = key.FromTradeType(tradeType);
            var tradePosition = GetTradePosition(tradeType, tradePositions);
            return tradePosition with
            {
                OrderId = id.OrderId,
                TradeId = id.TradeId,
                TradeType = id.TradeType,
                ValueDate = id.ValueDate,
                DaysToExpiry = id.DaysToExpiry,
                TradeStatus = id.TradeStatus,
                AssetPrice = assetPrice,
                RiskFreeRate = riskFreeRate,
                Commission = 0m
            };
        }

        TradePositionReadModel GetTradePosition(TradeType tradeType, ITradePositionCollection tradePositions)
        {
            var tradePosition = tradePositions.IntraDay(tradeType)?.ToViewModel();
            if (tradePosition is not null) return tradePosition;
            tradePosition = tradePositions.EndOfDay(tradeType)?.ToViewModel();
            if (tradePosition is not null) return tradePosition;
            return tradePositions.Opening(tradeType)!.ToViewModel();
        }

    }

}
