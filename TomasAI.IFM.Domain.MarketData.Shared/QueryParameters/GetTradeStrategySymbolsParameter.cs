using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;

public sealed record GetTradeStrategySymbolsParameter(TradeStrategyFamilyType Family) : IQueryParameter
{
    public string QueryParams => $"family={Family}";
}
