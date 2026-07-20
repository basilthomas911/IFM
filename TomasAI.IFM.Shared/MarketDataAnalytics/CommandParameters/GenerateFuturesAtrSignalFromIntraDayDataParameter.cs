using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;

/// <summary>
/// Represents the parameters required to generate a futures ATR signal from intra-day data.
/// </summary>
/// <param name="FuturesAtrSignalId">Identifier describing the target futures contract and value date for ATR signal generation.</param>
/// <param name="FuturesIntraDayData">Collection of intra-day data used as input for computing the ATR signal.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record GenerateFuturesAtrSignalFromIntraDayDataParameter(
    FuturesAtrSignalId FuturesAtrSignalId,
    FuturesIntraDayDataReadModel[] FuturesIntraDayData,
    int ErrorCode)
    : ICommandParameter;
