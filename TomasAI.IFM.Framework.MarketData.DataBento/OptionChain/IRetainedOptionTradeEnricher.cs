using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Optional retained-trade boundary. Await completion before acknowledging an observed source trade.</summary>
public interface IRetainedOptionTradeEnricher
{
    ValueTask<OptionGreeksSnapshot> EnrichTradeAsync(DatabentoOptionChainRoute route, LastTradeTickSnapshot tick,
        long eventNanoseconds, long receiveNanoseconds, CancellationToken cancellationToken);
}
