using System.Collections;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradePositionCollection : ITradePositionCollection
{
    List<ITradePosition> _tradePosition;

    public TradePositionCollection() =>  _tradePosition = new List<ITradePosition>();

    public int Count => _tradePosition.Count;

    public ITradePosition? this[TradePositionEntityId key]
        => _tradePosition
            .Where(e => 
                e.OrderId == key.OrderId
                && e.TradeId == key.TradeId
                && e.TradeType == key.TradeType
                && e.ValueDate == key.ValueDate
                && e.DaysToExpiry == key.DaysToExpiry
                && e.TradeStatus == key.TradeStatus)
            .SingleOrDefault();

    public ITradePosition New(TradePositionEntityId key, DateTime createdOn, string createdBy)
       => new TradePosition(
           orderId: key.OrderId,
           tradeId: key.TradeId,
           tradeType: key.TradeType,
           valueDate: key.ValueDate,
           daysToExpiry: key.DaysToExpiry,
           tradeStatus: key.TradeStatus,
           createdOn: createdOn,
           createdBy: createdBy);

    public ITradePosition[] Opening()
        => _tradePosition.Where(e => e.TradeStatus == TradeStatus.Open).ToArray();

    public ITradePosition? Opening(TradeType tradeType)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.Open).FirstOrDefault();

    public ITradePosition? IntraDay(TradeType tradeType)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.IntraDay).LastOrDefault();

    public ITradePosition? IntraDay(TradeType tradeType, DateOnly valueDate)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.IntraDay && e.ValueDate == valueDate).LastOrDefault();

    public ITradePosition? EndOfDay(TradeType tradeType)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.EndOfDay).LastOrDefault();

    public ITradePosition? EndOfDay(TradeType tradeType, DateOnly valueDate)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.EndOfDay && e.ValueDate == valueDate).LastOrDefault();

    public ITradePosition? Closing(TradeType tradeType)
        => _tradePosition.Where(e => e.TradeType == tradeType && e.TradeStatus == TradeStatus.Close).FirstOrDefault();


    public TradePositionChangeSourceType Source(TradeType tradeType)
    {
        switch (tradeType)
        {
            case TradeType.PutCreditSpread:
                return TradePositionChangeSourceType.PutCreditSpreadLeg;
            case TradeType.CallCreditSpread:
                return TradePositionChangeSourceType.CallCreditSpreadLeg;
        }
        return TradePositionChangeSourceType.None;
    }

    public bool Exists(TradePositionEntityId key)
        => _tradePosition.Exists(e => 
                        e.OrderId == key.OrderId
                      && e.TradeId == key.TradeId
                      && e.TradeType == key.TradeType
                      && e.ValueDate == key.ValueDate
                      && e.DaysToExpiry == key.DaysToExpiry
                      && e.TradeStatus == key.TradeStatus);

    public void Add(ITradePosition spreadTradeData) => _tradePosition.Add(spreadTradeData);

    public void Clear() => _tradePosition.Clear();

    public IEnumerator<ITradePosition> GetEnumerator() => _tradePosition.GetEnumerator();

    public bool Remove(ITradePosition spreadTradeData) => _tradePosition.Remove(spreadTradeData);

    IEnumerator IEnumerable.GetEnumerator() => _tradePosition.GetEnumerator();

    public ITradePositionCollection SetTradePnl(TradeType tradeType)
    {
        var yesterdayTradeData = EndOfDay(tradeType) ?? Opening(tradeType);
        var intraDayTradeData = IntraDay(tradeType);
        if (!(yesterdayTradeData == null || intraDayTradeData == null))
            intraDayTradeData.SetTradePnl(yesterdayTradeData.TradeValue - intraDayTradeData.TradeValue);
        return this;
    }

    public ITradePositionCollection SetClosingTradePnl(TradeType tradeType, decimal openingTradeValue)
    {
        var closingTradeData = Closing(tradeType);
        var intraDayTradeData = IntraDay(tradeType);
        if (!(closingTradeData == null || intraDayTradeData == null))
            intraDayTradeData.SetTradePnl(openingTradeValue - closingTradeData.TradeValue);
        return this;
    }

}
