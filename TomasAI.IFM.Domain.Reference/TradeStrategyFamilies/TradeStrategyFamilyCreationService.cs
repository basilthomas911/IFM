// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies;

public sealed class TradeStrategyFamilyCreationService(IMarketDataApi marketData, ITradeStrategyFamilyCatalogStore store, TimeProvider timeProvider)
{
    public async Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, string principal, CancellationToken cancellationToken = default)
        => await store.CreateAsync(request, await CandidateAsync(request, principal, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    public async Task<TradeStrategyFamilyReadModel> ChangeAsync(ChangeTradeStrategyFamilyRequest request, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate().Count != 0) throw new ArgumentException(string.Join("; ", request.Validate()));
        return await store.ChangeAsync(request, await CandidateAsync(request.Definition, principal, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public Task<TradeStrategyFamilyReadModel> RemoveAsync(RemoveTradeStrategyFamilyRequest request, string principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate().Count != 0) throw new ArgumentException(string.Join("; ", request.Validate()));
        if (string.IsNullOrWhiteSpace(principal)) throw new ArgumentException("Audit principal is required.");
        return store.RemoveAsync(request, timeProvider.GetUtcNow().UtcDateTime, principal, cancellationToken);
    }

    async Task<TradeStrategyFamilyReadModel> CandidateAsync(CreateTradeStrategyFamilyRequest request, string principal, CancellationToken cancellationToken)
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
        return candidate;
    }
}
