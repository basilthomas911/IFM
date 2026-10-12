using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public interface ITradeStrategySymbolSource
{
    Task<IReadOnlyList<TradeStrategyProduct>> DiscoverAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken);
}
