using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to start a market data feed.
/// </summary>
/// <param name="FuturesContracts">The futures contracts for which the market data feed should be started.</param>
/// <param name="ValueDate">The value (trading) date for the market data feed.</param>
/// <param name="ResetStream">True to reset any existing stream before starting; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartMarketDataFeedParameter(FuturesContractV2ReadModel[] FuturesContracts, DateOnly ValueDate, bool ResetStream, int ErrorCode)
    : ICommandParameter;
