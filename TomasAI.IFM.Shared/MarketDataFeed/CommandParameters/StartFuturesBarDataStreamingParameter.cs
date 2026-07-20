using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to start futures bar data streaming.
/// </summary>
/// <param name="FuturesContracts">The futures contracts to stream bar data for.</param>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesBarDataStreamingParameter(FuturesContractV2ReadModel[] FuturesContracts, DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
