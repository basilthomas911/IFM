using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetTradeStrategySymbolsQuery"/>.</summary>
public static class GetTradeStrategySymbols
{
    /// <summary>Reads and replies with symbols for the requested strategy family.</summary>
    public static async ValueTask ExecuteAsync(this GetTradeStrategySymbolsQuery query, IReferenceQueryContext context, CancellationToken cancellationToken)
    {
        var result = context.MarketDataApi is null
            ? new ServiceFailed<TradeStrategySymbolReadModel[]>(GetTradeStrategySymbolsQuery.ErrorId, "Market-data API is unavailable.")
            : await context.MarketDataApi.GetTradeStrategySymbolsAsync(query.Family, cancellationToken);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result);
    }
}