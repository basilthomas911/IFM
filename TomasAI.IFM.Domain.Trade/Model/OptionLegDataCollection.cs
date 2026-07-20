using System.Collections;
using TomasAI.IFM.Shared.Trade;

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
        => _optionLegData
            .Where(e => e.TradeId == _tradeId 
                && e.TradeType == _tradeType
                && e.ValueDate == _valueDate 
                && e.DaysToExpiry == _daysToExpiry
                && e.TradeStatus == _tradeStatus
                && e.OptionLegId == contractId)
            .FirstOrDefault();

    IOptionLegData? this[OptionLegAction optionLegAction]
        => _optionLegData
            .Where(e => e.OptionLegAction == optionLegAction)
            .FirstOrDefault();

    public bool Exists(string contractId)
        => _optionLegData.Any(e => e.TradeId == _tradeId
                && e.TradeType == _tradeType
                && e.ValueDate == _valueDate
                && e.DaysToExpiry == _daysToExpiry
                && e.TradeStatus == _tradeStatus
                && e.OptionLegId == contractId);

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
