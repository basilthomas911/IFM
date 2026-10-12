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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1043", Justification = "This existing indexer deliberately accepts the typed composite trade identity.")]
    OptionTradeReadModel this[OptionTradeEntityId key] { get; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    OptionTradeReadModel[] this[string optionLegContractId] { get; }

    bool Exists(OptionTradeEntityId key);
    void Add(OptionTradeReadModel optionTrade);
    void Clear();
    bool Remove(OptionTradeReadModel optionTrade);
}
