using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade;

public interface IOptionTradeCollection : IEnumerable<OptionTradeReadModel>
{
    int Count { get; }
    OptionTradeReadModel this[OptionTradeEntityId key] { get; }
    OptionTradeReadModel PrimaryTrade { get; }

    bool Exists(OptionTradeEntityId key);
    void Add(OptionTradeReadModel optionTrade);
    void Clear();
    bool Remove(OptionTradeReadModel optionTrade);
}
