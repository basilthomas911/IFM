using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Contracts;

public interface IOptionTradeLiveFeedMap
{
    int Count { get; }
    OptionTradeReadModel this[OptionTradeEntityId key] { get; }
    OptionTradeReadModel[] this[string optionLegContractId] { get; }

    bool Exists(OptionTradeEntityId key);
    void Add(OptionTradeReadModel optionTrade);
    void Clear();
    bool Remove(OptionTradeReadModel optionTrade);
}
