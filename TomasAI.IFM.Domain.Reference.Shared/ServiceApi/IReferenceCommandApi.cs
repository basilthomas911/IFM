using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

    public interface IReferenceCommandApi
{
    Task<ServiceResult<Guid>> ExecuteStrategyCatalogAsync(StrategyCatalog.CatalogCommandRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("ConfigurationDb strategy catalog commands are unavailable.");
    Task<ServiceResult<Guid>> ChangeTradeStrategyFamilyAsync(ChangeTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Trade strategy family changes are not implemented by this adapter.");
    Task<ServiceResult<Guid>> RemoveTradeStrategyFamilyAsync(RemoveTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Trade strategy family removal is not implemented by this adapter.");
    Task<ServiceResult<Guid>> CreateTradeStrategyFamilyAsync(CreateTradeStrategyFamilyRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Trade strategy family creation is not implemented by this adapter.");
    Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType);
    Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite);
    Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite);
}
