using System.Collections;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

public class OptionLegDataCollection(
    int tradeId,
    TradeType tradeType,
    DateOnly valueDate,
    int daysToExpiry,
    TradeStatus tradeStatus,
    decimal assetPrice) : IOptionLegDataCollection
{
    readonly int _tradeId = tradeId;
    readonly TradeType _tradeType = tradeType;
    readonly DateOnly _valueDate = valueDate;
    readonly int _daysToExpiry = daysToExpiry;
    readonly TradeStatus _tradeStatus = tradeStatus;
    readonly decimal _assetPrice = assetPrice;
    readonly List<IOptionLegData> _optionLegData = [];

    public int Count => _optionLegData.Count;

    public double TradeMultiplier => (this[OptionLegAction.Short]?.Quantity ?? 0) * 50;

    public IOptionLegData? this[string contractId]
    {
        get
        {
            foreach (var optionLegData in _optionLegData)
                if (optionLegData.TradeId == _tradeId
                && optionLegData.TradeType == _tradeType
                && optionLegData.ValueDate == _valueDate
                && optionLegData.DaysToExpiry == _daysToExpiry
                && optionLegData.TradeStatus == _tradeStatus
                && optionLegData.OptionLegId == contractId)
                    return optionLegData;
            return null;
        }
    }

    IOptionLegData? this[OptionLegAction optionLegAction]
    {
        get
        {
            foreach (var optionLegData in _optionLegData)
                if (optionLegData.OptionLegAction == optionLegAction)
                    return optionLegData;
            return null;
        }
    }

    public bool Exists(string contractId)
        => this[contractId] is not null;

    public void Add(IOptionLegData item)
        => _optionLegData.Add(item);

    public void Remove(string contractId)
    {
        var optionLegData = this[contractId];
        if (optionLegData is not null && _optionLegData.Contains(optionLegData))
            _optionLegData.Remove(optionLegData);
    }

    public void Clear()
        => _optionLegData.Clear();

    public IEnumerator<IOptionLegData> GetEnumerator()
        => _optionLegData.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)_optionLegData).GetEnumerator();

    public decimal GetNetSpread()
    {
        var shortOptionData = this[OptionLegAction.Short];
        var longOptionData = this[OptionLegAction.Long];
        return (shortOptionData == null || longOptionData == null)
            ? 0: ((shortOptionData.BidPrice + shortOptionData.AskPrice) / 2)
                - ((longOptionData.BidPrice + longOptionData.AskPrice) / 2);
    }

    public decimal GetTradeValue()
    {
        var netSpread = GetNetSpread();
        var shortOptionData = this[OptionLegAction.Short];
        var tradeValue =  (shortOptionData == null) 
            ? 0: netSpread * shortOptionData.Quantity * 50;
        return tradeValue;
    }

    public double GetOTMProbability()
    {
        var otmProbability = 0.0;
        var shortOptionData = this[OptionLegAction.Short];
        if (shortOptionData != null)
        {
            var assetPrice = Convert.ToDouble(_assetPrice);
            otmProbability = shortOptionData.GetOTMProbability(assetPrice);
        }
        return otmProbability;
    }
   
}
