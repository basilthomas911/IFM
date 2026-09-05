using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record TradeStrategyProduct(TradeStrategyFamilyType Family, string Symbol, string Currency, string Exchange)
{
    public string Description => $"{Symbol} {(Family == TradeStrategyFamilyType.Futures ? "futures" : "futures options")}";
    public TradeStrategySymbolReadModel WithId(int id) => new() { Id = id, Symbol = Symbol, Currency = Currency, Exchange = Exchange, Description = Description };
    public void Validate()
    {
        if (Family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            throw new ArgumentException($"Unsupported trade strategy family: {Family}.");
        var errors = WithId(1).Validate();
        if (errors.Count != 0) throw new ArgumentException($"Invalid product '{Symbol}': {string.Join("; ", errors)}");
    }
}

public interface ITradeStrategySymbolSource
{
    Task<IReadOnlyList<TradeStrategyProduct>> DiscoverAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken);
}

public interface ITradeStrategySymbolStore
{
    Task<TradeStrategySymbolReadModel> GetOrCreateAsync(TradeStrategyProduct product, CancellationToken cancellationToken);
    Task<TradeStrategySymbolReadModel?> FindAsync(TradeStrategyFamilyType family, int id, CancellationToken cancellationToken);
}

public interface ITradeStrategySymbolCatalog
{
    Task<ServiceResult<TradeStrategySymbolReadModel[]>> GetAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken = default);
}
