using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>
/// Phase B supplies the Black-76 implementation and immutable session rate.
/// The Phase A session runtime depends only on this synchronous boundary.
/// </summary>
public interface IOptionChainGreeksEnricher
{
    OptionGreeksSnapshot EnrichQuote(
        DatabentoOptionChainRoute route,
        LastQuoteTickSnapshot tick);
    OptionGreeksSnapshot EnrichTrade(
        DatabentoOptionChainRoute route,
        LastTradeTickSnapshot tick);
}
