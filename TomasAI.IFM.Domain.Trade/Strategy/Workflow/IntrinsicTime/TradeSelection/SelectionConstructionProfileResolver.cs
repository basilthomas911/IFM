using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public interface ISelectionConstructionProfileResolver
{
    Task<SelectionConstructionProfileReference> ResolveAsync(SelectionPipelinePolicyReference reference,CatalogKey deployment,IReadOnlyDictionary<CatalogKey,SelectionCatalogDefinitionSnapshot> graph,DateTime at,CancellationToken cancellationToken=default);
}
public sealed class SelectionConstructionProfileResolver(IConfigurationDbContext configuration):ISelectionConstructionProfileResolver
{
    public async Task<SelectionConstructionProfileReference> ResolveAsync(SelectionPipelinePolicyReference reference,CatalogKey deployment,IReadOnlyDictionary<CatalogKey,SelectionCatalogDefinitionSnapshot> graph,DateTime at,CancellationToken cancellationToken=default)
    {
        var row=await configuration.GetSelectionPipelinePolicyAsync(reference.Kind,reference.Id,reference.Version,cancellationToken).ConfigureAwait(false)
            ??throw new TradeSelectionValidationException("TS.CONFIG.MISSING","Construction policy is missing.");
        return SelectionConstructionProfileReference.FromFrozen(row,reference,deployment,graph,at);
    }
}
