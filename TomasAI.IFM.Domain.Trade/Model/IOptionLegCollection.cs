using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface IOptionLegCollection : IEnumerable<IOptionLeg>
{
    int Count { get; }
    IOptionLeg? this[OptionLegAction optionLegAction, OptionType optionType] { get; }
    bool Exists(string contractId);
    void Add(IOptionLeg item);
    void Clear();
}
