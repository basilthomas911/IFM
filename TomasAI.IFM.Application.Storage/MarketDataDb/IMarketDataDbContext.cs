using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Combines Market Data database read and write capabilities.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3444", Justification = "Keep the published context interface and named argument contract; inherited capability members intentionally describe the same database operation.")]
public interface IMarketDataDbContext :
    IObjectRepository<MarketDataDbContext>,
    IMarketDataDbReadContext,
    IMarketDataDbWriteContext,
    ICompositionPreparationStore,
    TomasAI.IFM.Application.MarketData.Contracts.IOptionChainStatisticsStore,
    TomasAI.IFM.Application.MarketData.Contracts.IStrategyOptionChainParameterStore,
    IOptionTradeEvidenceWriter,
    IOptionVolatilityRepository,
    IHistoricalObservationStore
{
    /// <summary>Reads canonical observations and compatible persisted Native replay coverage in one bounded request.</summary>
    Task<IReadOnlyList<FuturesTradeSessionBarReadModel>> GetQualifiedIndicatorSeedBarsAsync(string contractId, TimeFrameType frame,
        DateOnly valueDate, DateTimeOffset[] intervalEndsUtc, string sourceVersion, CancellationToken cancellationToken);

    /// <summary>Atomically retains independently qualified seed intervals for the same contract, date and timeframe.</summary>
    Task StoreQualifiedIndicatorSeedBarsAsync(string sourceVersion, FuturesTradeSessionBarReadModel[] bars, CancellationToken cancellationToken);

    /// <summary>Gets the Market Data database read capability.</summary>
    IMarketDataDbReadContext DbReader { get; }

    /// <summary>Gets the Market Data database write capability.</summary>
    IMarketDataDbWriteContext DbWriter { get; }
}
