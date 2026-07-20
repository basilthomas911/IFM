using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public interface ITradeStrategyType
    {
        string Name { get; }
        string Description { get; }
        TradeType TradeType { get; }
        ITradeStrategyRuleInfo GetRuleInfo(string ruleName);
    }
}
