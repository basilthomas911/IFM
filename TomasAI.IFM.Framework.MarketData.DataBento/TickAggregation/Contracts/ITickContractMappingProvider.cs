using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

public interface ITickContractMappingProvider
{
    bool TryGetMapping(
        string dataset,
        DateOnly definitionDate,
        InstrumentKey instrument,
        out TickContractMapping mapping);

    /// <summary>
    /// Resolves the instrument identity returned by a live feed registration.
    /// Implementations may use the requested/raw symbol as a definition-scoped
    /// fallback when provider metadata and the live session use different
    /// instrument identifiers for the same contract.
    /// </summary>
    bool TryResolveFeedMapping(
        string dataset,
        DateOnly definitionDate,
        TickerInstrumentRegistration registration,
        out TickContractMapping mapping) 
    {
        ArgumentNullException.ThrowIfNull(registration);
        return TryGetMapping(dataset, definitionDate, registration.Instrument, out mapping);
    }
}
