using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.OptionPricer.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public interface ISpreadDistributionStatus
    {
        bool IsBusy { get; }
        Task<int> CreateAsync(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate);
        Task ReleaseAsync(int statusId);
    }
}
