using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to start a market data feed.
/// </summary>
/// <param name="FuturesContracts">The futures contracts for which the market data feed should be started.</param>
/// <param name="ValueDate">The value (trading) date for the market data feed.</param>
/// <param name="ResetStream">True to reset any existing stream before starting; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
public record StartMarketDataFeedParameter(FuturesContractV3ReadModel[] FuturesContracts, DateOnly ValueDate, bool ResetStream, int ErrorCode)
    : ICommandParameter;
