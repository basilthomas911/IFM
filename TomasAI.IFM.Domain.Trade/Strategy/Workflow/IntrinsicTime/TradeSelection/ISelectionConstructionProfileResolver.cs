using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public interface ISelectionConstructionProfileResolver
{
    Task<SelectionConstructionProfileReference> ResolveAsync(SelectionPipelinePolicyReference reference, CatalogKey deployment, IReadOnlyDictionary<CatalogKey, SelectionCatalogDefinitionSnapshot> graph, DateTime at, CancellationToken cancellationToken = default);
}
