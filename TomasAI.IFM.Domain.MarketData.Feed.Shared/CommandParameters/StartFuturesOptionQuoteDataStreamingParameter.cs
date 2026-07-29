using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

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
