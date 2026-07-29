using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared;

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
