using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;

/// <summary>
/// Represents the parameters required to insert VIX futures end-of-day data.
/// </summary>
/// <param name="VixFuturesTickData">The VIX futures tick data to insert.</param>
/// <param name="ErrorCode">The error code associated with the operation.</param>
public record InsertVixFuturesEodDataParameter(FuturesTickDataV2ReadModel VixFuturesTickData, int ErrorCode)
    : ICommandParameter;
