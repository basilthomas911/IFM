using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Handles <see cref="GetTradeStrategySymbolsQuery"/>.</summary>
public static class GetTradeStrategySymbols
{
    /// <summary>Reads and replies with the symbols configured for a trade-strategy family.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeStrategySymbolsQuery query, IMarketDataQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = context.MarketDataApi is null
            ? new TomasAI.IFM.Shared.EventSourcing.ServiceFailed<TradeStrategySymbolReadModel[]>(503, "Market-data API is unavailable.")
            : await context.MarketDataApi.GetTradeStrategySymbolsAsync(query.Family, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }
}