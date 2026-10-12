using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public interface IOptionChainTransientEventPublisher
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Retain the published C# interface member and named argument contract.")]
    ValueTask PublishAsync(FuturesOptionChainQuoteChangedServiceEvent @event);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Retain the published C# interface member and named argument contract.")]
    ValueTask PublishAsync(FuturesOptionChainTradeChangedServiceEvent @event);
}
