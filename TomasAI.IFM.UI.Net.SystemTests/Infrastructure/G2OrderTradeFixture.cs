using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed record G2OrderTradeFixture(
    string PreferredBaseSymbol,
    string OrderReference,
    string TradeReference,
    TradeType TradeType,
    TradeState InitialTradeState,
    TradeState ChangedTradeState,
    int MaturityDays)
{
    public static G2OrderTradeFixture Create(G2Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new G2OrderTradeFixture(
            configuration.SecuritiesSymbol,
            $"{configuration.RunPrefix}-Order",
            $"{configuration.RunPrefix}-Trade",
            TradeType.ShortIronCondor,
            TradeState.NewTrade,
            TradeState.OrderSubmitted,
            30);
    }
}
