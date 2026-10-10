using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.UI.Net.ViewModels.Operations;

public enum StrategyWorkflowDetailState { NotStarted, Processing, Completed, Stopped, Failed }

public sealed record StrategyWorkflowDetailSection(
    string Key, string Title, string Summary, StrategyWorkflowDetailState State,
    string AccessibleStatus, string Content);

public sealed record StrategyWorkflowDetails(
    StrategyWorkflowId WorkflowId, long WorkflowRevision, string Header,
    IReadOnlyList<StrategyWorkflowDetailSection> Sections)
{
    /// <summary>Gets the immutable workflow objects inspected by the read-only Details browser.</summary>
    public TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView? Workflow { get; init; }
}
