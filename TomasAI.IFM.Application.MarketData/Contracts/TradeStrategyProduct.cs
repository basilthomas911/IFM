using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record TradeStrategyProduct(TradeStrategyFamilyType Family, string Symbol, string Currency, string Exchange)
{
    public string Description => $"{Symbol} {(Family == TradeStrategyFamilyType.Futures ? "futures" : "futures options")}";
    public TradeStrategySymbolReadModel WithId(int id) => new() { Id = id, Symbol = Symbol, Currency = Currency, Exchange = Exchange, Description = Description };
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "Instance validation reports the configuration property captured by this object.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "Instance validation reports the configuration property captured by this object.")]
    public void Validate()
    {
        if (Family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            throw new ArgumentException($"Unsupported trade strategy family: {Family}.");
        var errors = WithId(1).Validate();
        if (errors.Count != 0) throw new ArgumentException($"Invalid product '{Symbol}': {string.Join("; ", errors)}");
    }
}
