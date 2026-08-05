using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

public sealed class OptionTradeLiveFeedMapTests
{
    [Fact]
    public void ContractLookup_ReturnsOnlyTradesContainingTheOptionLeg()
    {
        var map = new OptionTradeLiveFeedMap();
        var matching = CreateTrade(1, "ES-CALL-5000");
        var other = CreateTrade(2, "ES-PUT-4800");
        map.Add(matching);
        map.Add(other);

        var result = map["ES-CALL-5000"];

        result.Should().ContainSingle().Which.Should().BeSameAs(matching);
        map["missing"].Should().BeEmpty();
    }

    static OptionTradeReadModel CreateTrade(int tradeId, string contractId)
        => new OptionTradeReadModel
        {
            OrderId = 10,
            TradeId = tradeId,
            TradeStrategy = "IronCondor"
        }.AddOptionLegs([
            OptionTradeLegReadModel.Default(
                10,
                tradeId,
                contractId,
                OptionType.Call,
                OptionLegAction.Long)
        ]);
}
