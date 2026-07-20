using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert futures option quote data.
/// </summary>
/// <param name="QuoteId">The quote identifier.</param>
/// <param name="ContractId">The contract identifier.</param>
/// <param name="QuoteData">The option quote data to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertFuturesOptionQuoteDataParameter(int QuoteId, string ContractId, QuoteData QuoteData, int ErrorCode)
    : ICommandParameter;
