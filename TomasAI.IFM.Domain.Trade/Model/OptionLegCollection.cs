using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System.Collections;

namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>
/// create option leg collection
/// </summary>
/// <param name="tradeId"></param>
public class OptionLegCollection(int tradeId) : IOptionLegCollection
{
    readonly int _tradeId = tradeId;
    readonly List<IOptionLeg> _optionLegs = [];

    /// <summary>
    /// count of option legs
    /// </summary>
    public int Count => _optionLegs!.Count;
    
    /// <summary>
    /// return selected option leg with collection
    /// </summary>
    /// <param name="optionLegAction"></param>
    /// <param name="optionType"></param>
    /// <returns></returns>
    public IOptionLeg? this[OptionLegAction optionLegAction, OptionType optionType]
    {
        get
        {
            IOptionLeg? result = null;
            foreach (var optionLeg in _optionLegs)
            {
                if (optionLeg.TradeId != _tradeId || optionLeg.OptionLegAction != optionLegAction || optionLeg.OptionLegType != optionType)
                    continue;
                if (result is not null)
                    throw new InvalidOperationException("Sequence contains more than one matching element");
                result = optionLeg;
            }
            return result;
        }
    }

    /// <summary>
    /// chekc if selected option leg exists with collection
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public bool Exists(string contractId)
    {
        foreach (var optionLeg in _optionLegs)
            if (optionLeg.TradeId == _tradeId && optionLeg.ContractId == contractId)
                return true;
        return false;
    }

    /// <summary>
    /// add option leg to collection
    /// </summary>
    /// <param name="item"></param>
    public void Add(IOptionLeg item) => _optionLegs.Add(item);

    /// <summary>
    /// clear option leg collection
    /// </summary>
    public void Clear() => _optionLegs.Clear();

    /// <summary>
    /// return option leg collection enumerator
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IOptionLeg> GetEnumerator() => _optionLegs.GetEnumerator();

    /// <summary>
    /// return option leg enumerator
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_optionLegs).GetEnumerator();

}
