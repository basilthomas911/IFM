using System.Reactive.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Service.AlgoTrader.Model.LongIronCondor;

namespace TomasAI.IFM.Service.AlgoTrader.UnitTests
{
   
    public class LongIronCondorAlgoTests
    {
        [Fact]
        public void RaiseTrailingStopLimitOk()
        {
            var tradePlan = SampleData.TradePlan with { TradePnl = 1024.45m };
            var algo = new LongIronCondorAlgorithm(tradePlan);
            var ruleEngine = new LongIronCondorRuleEngine(algo);
            ruleEngine.Match(ActionSubType.RaiseTrailingStopLimit).Should().BeTrue();
        }
    }
}
