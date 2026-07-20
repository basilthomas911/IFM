using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public record TradeStrategyRuleInfo(string Name, string Description) : ITradeStrategyRuleInfo
    {
        public override string ToString() => $"{Name}{Environment.NewLine}{Description}";
    }
}
