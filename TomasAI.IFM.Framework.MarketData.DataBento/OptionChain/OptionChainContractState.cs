using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Initializes a new OptionChainContractState instance.</summary>
/// <param name="Route">The route.</param>
/// <param name="Quote">The bid and ask snapshot, when available.</param>
/// <param name="Trade">The last trade snapshot, when available.</param>
/// <param name="SessionVolume">The session volume.</param>
/// <param name="OpenInterest">The open interest.</param>
/// <param name="StatisticsAtUtc">The statistics at utc.</param>
/// <param name="SessionVolumeOfficial">The session volume official.</param>
public readonly record struct OptionChainContractState(
    DatabentoOptionChainRoute Route,
    LastQuoteTickWithGreeksSnapshot? Quote,
    LastTradeTickWithGreeksSnapshot? Trade,
    long? SessionVolume = null,
    long? OpenInterest = null,
    DateTimeOffset? StatisticsAtUtc = null,
    bool SessionVolumeOfficial = false);
