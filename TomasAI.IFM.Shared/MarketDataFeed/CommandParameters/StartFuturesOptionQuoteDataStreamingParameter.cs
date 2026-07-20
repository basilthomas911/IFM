using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to start futures option quote data streaming.
/// </summary>
/// <param name="QuoteId">The quote identifier.</param>
/// <param name="FuturesOptionQuotes">The futures option quotes to stream.</param>
/// <param name="FuturesOptionContracts">The futures option contracts associated with the quotes.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StartFuturesOptionQuoteDataStreamingParameter(
    int QuoteId,
    FuturesOptionQuoteReadModel[] FuturesOptionQuotes,
    FuturesOptionContractReadModel[] FuturesOptionContracts,
    int ErrorCode)
    : ICommandParameter;
