using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to start futures tick data streaming.
/// </summary>
/// <param name="FuturesContract">The futures contract to stream tick data for.</param>
/// <param name="ValueDate">The value (trading) date.</param>
/// <param name="ResetStream">True to reset the stream before starting; otherwise false.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesTickDataStreamingParameter(FuturesContractV2ReadModel FuturesContract, DateOnly ValueDate, bool ResetStream, int ErrorCode)
    : ICommandParameter;
