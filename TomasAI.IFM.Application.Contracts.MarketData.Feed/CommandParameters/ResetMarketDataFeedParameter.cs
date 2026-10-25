using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to reset a market data feed.
/// </summary>
/// <param name="FuturesContracts">The futures contracts to reset in the market data feed.</param>
/// <param name="ValueDate">The target value (trading) date for which the feed reset should be applied.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
public record ResetMarketDataFeedParameter(FuturesContractV3ReadModel[] FuturesContracts, DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
