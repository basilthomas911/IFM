using System.Collections;
using TomasAI.IFM.Shared.Trade;

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
        => _optionLegs.SingleOrDefault(e => e.TradeId == _tradeId && e.OptionLegAction == optionLegAction && e.OptionLegType == optionType);

    /// <summary>
    /// chekc if selected option leg exists with collection
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public bool Exists(string contractId) => _optionLegs.Any(e => e.TradeId == _tradeId && e.ContractId == contractId);

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
