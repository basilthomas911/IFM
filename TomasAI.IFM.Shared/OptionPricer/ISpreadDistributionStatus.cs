using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface ISpreadDistributionStatus
    {
        bool IsBusy { get; }
        Task<int> CreateAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate);
        Task ReleaseAsync(int statusId);
    }
}
