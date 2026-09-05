using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies;

public sealed class TradeStrategyFamilyCreationService(IMarketDataApi marketData, ITradeStrategyFamilyCatalogStore store, TimeProvider timeProvider)
{
    public async Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = request.Validate();
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
        if (string.IsNullOrWhiteSpace(principal)) throw new ArgumentException("Audit principal is required.");
        var symbols = await marketData.GetTradeStrategySymbolsAsync(request.Family, cancellationToken).ConfigureAwait(false);
        if (!symbols.Success || symbols.Value is null) throw new InvalidOperationException(symbols.ErrorMessage ?? "Symbol catalog is unavailable.");
        var product = symbols.Value.SingleOrDefault(x => x.Id == request.TradeStrategySymbolId)
            ?? throw new ArgumentException("The selected product is not available for this family.");
        if (product.Validate().Count != 0) throw new InvalidOperationException("Incomplete product metadata cannot create a family.");
        var candidate = new TradeStrategyFamilyReadModel
        {
            Family = request.Family, Strategy = request.Strategy, TimeFrame = request.TimeFrame,
            SystemKey = TradeStrategyFamilyReadModel.ComposeSystemKey(request.Family, request.Strategy),
            TradeStrategySymbolId = product.Id, Symbol = product.Symbol, Currency = product.Currency, Exchange = product.Exchange,
            Description = request.Description.Trim(), CreatedOnUtc = timeProvider.GetUtcNow().UtcDateTime, CreatedBy = principal
        };
        return await store.CreateAsync(request, candidate, cancellationToken).ConfigureAwait(false);
    }
}
