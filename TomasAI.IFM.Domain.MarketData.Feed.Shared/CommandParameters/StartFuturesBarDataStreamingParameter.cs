using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to start futures bar data streaming.
/// </summary>
/// <param name="FuturesContracts">The futures contracts to stream bar data for.</param>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesBarDataStreamingParameter(FuturesContractV3ReadModel[] FuturesContracts, DateOnly ValueDate, int ErrorCode)
    : ICommandParameter;
