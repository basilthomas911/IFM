using System.Collections;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradePositionCollection : ITradePositionCollection
{
    readonly List<ITradePosition> _tradePosition;

    public TradePositionCollection() =>  _tradePosition = new List<ITradePosition>();

    public int Count => _tradePosition.Count;

    public ITradePosition? this[TradePositionEntityId key]
    {
        get
        {
            ITradePosition? match = null;
            foreach (var position in _tradePosition)
            {
                if (!Matches(position, key))
                    continue;
                if (match is not null)
                    throw new InvalidOperationException("Sequence contains more than one matching element");
                match = position;
            }
            return match;
        }
    }

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
    {
        var result = new List<ITradePosition>();
        foreach (var position in _tradePosition)
            if (position.TradeStatus == TradeStatus.Open)
                result.Add(position);
        return [.. result];
    }

    public ITradePosition? Opening(TradeType tradeType)
        => FindFirst(tradeType, TradeStatus.Open);

    public ITradePosition? IntraDay(TradeType tradeType)
        => FindLast(tradeType, TradeStatus.IntraDay);

    public ITradePosition? IntraDay(TradeType tradeType, DateOnly valueDate)
        => FindLast(tradeType, TradeStatus.IntraDay, valueDate);

    public ITradePosition? EndOfDay(TradeType tradeType)
        => FindLast(tradeType, TradeStatus.EndOfDay);

    public ITradePosition? EndOfDay(TradeType tradeType, DateOnly valueDate)
        => FindLast(tradeType, TradeStatus.EndOfDay, valueDate);

    public ITradePosition? Closing(TradeType tradeType)
        => FindFirst(tradeType, TradeStatus.Close);


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
    {
        foreach (var position in _tradePosition)
            if (Matches(position, key))
                return true;
        return false;
    }

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

    ITradePosition? FindFirst(TradeType tradeType, TradeStatus tradeStatus)
    {
        foreach (var position in _tradePosition)
            if (position.TradeType == tradeType && position.TradeStatus == tradeStatus)
                return position;
        return null;
    }

    ITradePosition? FindLast(TradeType tradeType, TradeStatus tradeStatus, DateOnly? valueDate = null)
    {
        for (var index = _tradePosition.Count - 1; index >= 0; index--)
        {
            var position = _tradePosition[index];
            if (position.TradeType == tradeType
                && position.TradeStatus == tradeStatus
                && (!valueDate.HasValue || position.ValueDate == valueDate.Value))
                return position;
        }
        return null;
    }

    static bool Matches(ITradePosition position, TradePositionEntityId key)
        => position.OrderId == key.OrderId
            && position.TradeId == key.TradeId
            && position.TradeType == key.TradeType
            && position.ValueDate == key.ValueDate
            && position.DaysToExpiry == key.DaysToExpiry
            && position.TradeStatus == key.TradeStatus;

}
