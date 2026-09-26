using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Combines Market Data database read and write capabilities.
/// </summary>
public interface IMarketDataDbContext :
    IObjectRepository<MarketDataDbContext>,
    IMarketDataDbReadContext,
    IMarketDataDbWriteContext,
    ICompositionPreparationStore,
    IOptionTradeEvidenceWriter,
    IOptionVolatilityRepository,
    IHistoricalObservationStore
{
    /// <summary>Gets the Market Data database read capability.</summary>
    IMarketDataDbReadContext DbReader { get; }

    /// <summary>Gets the Market Data database write capability.</summary>
    IMarketDataDbWriteContext DbWriter { get; }
}
