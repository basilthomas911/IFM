using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Query;

public sealed class LegacyPortfolioHistoryReadModelTests
{
    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Match_classification_uses_TradeDb_execution_evidence_in_precedence_order()
    {
        LegacyFundTradeHistoryReadModel.Classify(null).Should().Be(LegacyTradeMatchStatus.NoTradeDbDefinition);
        LegacyFundTradeHistoryReadModel.Classify(Trade()).Should().Be(LegacyTradeMatchStatus.DefinitionOnly);
        LegacyFundTradeHistoryReadModel.Classify(Trade().AddTradePosition([new TradePositionReadModel
        {
            OrderId = 10, TradeId = 20, ValueDate = new DateOnly(2025, 1, 2), TradeType = TradeType.ShortIronCondor,
        }])).Should().Be(LegacyTradeMatchStatus.PositionHistory);
        LegacyFundTradeHistoryReadModel.Classify(Trade()
            .AddTradePosition([new TradePositionReadModel { OrderId = 10, TradeId = 20, ValueDate = new DateOnly(2025, 1, 2) }])
            .AddTradeFills([new TradeFillReadModel(10, 20, DateTime.UtcNow, 1, DateTime.UtcNow, "test")]))
            .Should().Be(LegacyTradeMatchStatus.FillHistory);
    }

    static OptionTradeReadModel Trade() => new() { OrderId = 10, TradeId = 20 };
}
