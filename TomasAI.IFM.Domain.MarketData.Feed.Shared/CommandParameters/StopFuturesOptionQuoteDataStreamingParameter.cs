using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to stop futures option quote data streaming.
/// </summary>
/// <param name="QuoteId">The quote identifier to stop streaming for.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record StopFuturesOptionQuoteDataStreamingParameter(int QuoteId, int ErrorCode)
    : ICommandParameter;
