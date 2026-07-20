using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Service.AlgoTrader.Reactive
{
    public interface ITradeStrategyRuleInfo
    {
        string Name { get; }
        string Description { get; }
    }
}
