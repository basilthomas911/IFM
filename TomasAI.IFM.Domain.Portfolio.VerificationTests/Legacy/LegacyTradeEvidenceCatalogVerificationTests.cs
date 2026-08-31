using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Legacy;

public sealed class LegacyTradeEvidenceCatalogVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Every_minimum_TradeDb_evidence_combination_has_one_stable_operator_label()
    {
        var cases = new (OptionTradeReadModel? Trade, LegacyTradeMatchStatus Expected)[]
        {
            (null, LegacyTradeMatchStatus.NoTradeDbDefinition),
            (new OptionTradeReadModel { OrderId = 1, TradeId = 1 }, LegacyTradeMatchStatus.DefinitionOnly),
            (new OptionTradeReadModel { OrderId = 1, TradeId = 2 }.AddTradePosition([new TradePositionReadModel()]), LegacyTradeMatchStatus.PositionHistory),
            (new OptionTradeReadModel { OrderId = 1, TradeId = 3 }.AddTradeFills([new TradeFillReadModel()]), LegacyTradeMatchStatus.FillHistory),
        };

        cases.Select(x => LegacyFundTradeHistoryReadModel.Classify(x.Trade)).Should().Equal(cases.Select(x => x.Expected));
        cases.Select(x => x.Expected).Should().OnlyHaveUniqueItems();
    }
}
