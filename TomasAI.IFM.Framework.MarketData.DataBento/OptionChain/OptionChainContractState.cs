using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

/// <summary>Initializes a new OptionChainContractState instance.</summary>
/// <param name="Route">The route.</param>
/// <param name="Quote">The bid and ask snapshot, when available.</param>
/// <param name="Trade">The last trade snapshot, when available.</param>
/// <param name="SessionVolume">The session volume.</param>
/// <param name="OpenInterest">The open interest.</param>
/// <param name="StatisticsAtUtc">The statistics at utc.</param>
/// <param name="SessionVolumeOfficial">Whether volume is the provider's official cleared total for its reference date.</param>
/// <param name="VolumeValueDate">The trading date represented by volume.</param>
/// <param name="OpenInterestValueDate">The exchange reference date represented by open interest.</param>
/// <param name="VolumeObservedAtUtc">The last accepted provider volume-statistic event time.</param>
/// <param name="OpenInterestObservedAtUtc">The last accepted provider open-interest event time.</param>
/// <param name="ObservedSessionVolume">Deduplicated trade size accumulated during this session's replay and live stream.</param>
/// <param name="LastVolumeEventNanoseconds">The accepted trade-time watermark, retained through physical subscription expansion.</param>
/// <param name="LastVolumeSequence">The sequence at the accepted trade-time watermark.</param>
/// <param name="VolumeReplayComplete">Whether the provider has replayed the full trading session for this instrument.</param>
public readonly record struct OptionChainContractState(
    DatabentoOptionChainRoute Route,
    LastQuoteTickWithGreeksSnapshot? Quote,
    LastTradeTickWithGreeksSnapshot? Trade,
    long? SessionVolume = null,
    long? OpenInterest = null,
    DateTimeOffset? StatisticsAtUtc = null,
    bool SessionVolumeOfficial = false,
    DateOnly? VolumeValueDate = null,
    DateOnly? OpenInterestValueDate = null,
    DateTimeOffset? VolumeObservedAtUtc = null,
    DateTimeOffset? OpenInterestObservedAtUtc = null,
    long ObservedSessionVolume = 0,
    long LastVolumeEventNanoseconds = long.MinValue,
    uint LastVolumeSequence = 0,
    bool VolumeReplayComplete = false);
