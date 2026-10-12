using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

public interface IRegimeDiscoveryDecisionReferenceCsvAdapter
{
    Task ExportAsync(IReadOnlyCollection<RegimeDiscoveryDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default);
}
