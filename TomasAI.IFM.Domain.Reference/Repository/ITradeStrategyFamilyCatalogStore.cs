

// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.

using TomasAI.IFM.Domain.Reference.Shared.ViewModels;



namespace TomasAI.IFM.Domain.Reference.Repository;

public interface ITradeStrategyFamilyCatalogStore
{
    Task<TradeStrategyFamilyReadModel> CreateAsync(CreateTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken);
    Task<TradeStrategyFamilyReadModel> ChangeAsync(ChangeTradeStrategyFamilyRequest request, TradeStrategyFamilyReadModel candidate, CancellationToken cancellationToken);
    Task<TradeStrategyFamilyReadModel> RemoveAsync(RemoveTradeStrategyFamilyRequest request, DateTime removedUtc, string principal, CancellationToken cancellationToken);
}
