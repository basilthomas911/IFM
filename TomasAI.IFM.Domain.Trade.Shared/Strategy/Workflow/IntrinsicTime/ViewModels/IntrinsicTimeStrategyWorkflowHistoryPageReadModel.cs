using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents one bounded page of workflow history for a symbol and timeframe.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
public sealed record IntrinsicTimeStrategyWorkflowHistoryPageReadModel(
    IntrinsicTimeStrategyWorkflowHistoryReadModel[] Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    /// <summary>Gets whether another page is available.</summary>
    public bool HasNextPage => PageNumber * PageSize < TotalCount;
}
