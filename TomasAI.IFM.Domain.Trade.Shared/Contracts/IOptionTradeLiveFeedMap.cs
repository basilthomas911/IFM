using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Contracts;

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
