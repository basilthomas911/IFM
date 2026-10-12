using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Storage;

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
    TomasAI.IFM.Application.MarketData.Contracts.IStrategyOptionChainParameterStore,
    IOptionTradeEvidenceWriter,
    IOptionVolatilityRepository,
    IHistoricalObservationStore
{
    /// <summary>Gets the Market Data database read capability.</summary>
    IMarketDataDbReadContext DbReader { get; }

    /// <summary>Gets the Market Data database write capability.</summary>
    IMarketDataDbWriteContext DbWriter { get; }
}
