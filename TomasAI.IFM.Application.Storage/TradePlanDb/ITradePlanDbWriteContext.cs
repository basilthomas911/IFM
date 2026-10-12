using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Defines Trade Plan database commands.
/// </summary>
public interface ITradePlanDbWriteContext
{
    /// <summary>Projects a plan inside the transaction that appends its source snapshot event.</summary>
    Task ProjectMaterialAsync(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default);

    /// <summary>Projects a material Trade Plan revision and its activity entry.</summary>
    /// <param name="plan">The material-plan snapshot to project.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
    /// <returns>A task representing the asynchronous projection operation.</returns>
    Task ProjectMaterialAsync(
        StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default);

    /// <summary>Projects an exit workflow inside its source event transaction.</summary>
    Task ProjectExitWorkflowAsync(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default);

    /// <summary>Projects an exit-position workflow revision.</summary>
    /// <param name="workflow">The workflow projection to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
    /// <returns>A task representing the asynchronous projection operation.</returns>
    Task ProjectExitWorkflowAsync(
        ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default);
}
