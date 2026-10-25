using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Initializes a new FuturesOptionChainQuoteChangedServiceEvent instance.</summary>
/// <param name="EventId">The event id.</param>
/// <param name="FuturesContractId">The futures contract id.</param>
/// <param name="FuturesOptionContractId">The futures option contract id.</param>
/// <param name="ValueDate">The trading value date associated with the data.</param>
/// <param name="MaturityDate">The maturity date.</param>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
public readonly record struct FuturesOptionChainQuoteChangedServiceEvent(
    Guid EventId,
    string FuturesContractId,
    string FuturesOptionContractId,
    DateOnly ValueDate,
    DateOnly MaturityDate,
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);
