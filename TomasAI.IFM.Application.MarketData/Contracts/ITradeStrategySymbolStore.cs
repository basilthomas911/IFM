using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public interface ITradeStrategySymbolStore
{
    Task<TradeStrategySymbolReadModel> GetOrCreateAsync(TradeStrategyProduct product, CancellationToken cancellationToken);
    Task<TradeStrategySymbolReadModel?> FindAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken);
}
