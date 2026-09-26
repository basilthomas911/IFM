using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Combines Market Data database read and write capabilities.
/// </summary>
public interface IMarketDataDbContext :
    IObjectRepository<MarketDataDbContext>,
    IMarketDataDbReadContext,
    IMarketDataDbWriteContext
{
    /// <summary>Gets the Market Data database read capability.</summary>
    IMarketDataDbReadContext DbReader { get; }

    /// <summary>Gets the Market Data database write capability.</summary>
    IMarketDataDbWriteContext DbWriter { get; }
}
