using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

public interface IMarketConditionDecisionReferenceCsvAdapter
{
    Task ExportAsync(IReadOnlyCollection<MarketConditionDecisionReferenceDto> results, string fileName,
        bool overwrite = true, CancellationToken cancellationToken = default);
}
