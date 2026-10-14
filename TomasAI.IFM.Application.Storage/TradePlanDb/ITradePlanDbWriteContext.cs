using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Defines Trade Plan database commands.
/// </summary>
public interface ITradePlanDbWriteContext
{
    /// <summary>Projects a plan inside the caller-owned source transaction; failures propagate to its owner.</summary>
    /// <param name="transaction">The transaction owning the source event append; this method never commits it.</param>
    /// <param name="plan">The immutable snapshot facts to persist.</param>
    /// <param name="cancellationToken">Cancels this single write attempt.</param>
    /// <returns>A task representing persistence of the plan and activity rows.</returns>
    Task ProjectMaterialAsync(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default);

    /// <summary>Attempts a standalone material snapshot and activity write once; failures are logged and dropped without replay or retry.</summary>
    /// <param name="plan">The material-plan snapshot to project.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
    /// <returns>A task representing the asynchronous projection operation.</returns>
    Task ProjectMaterialAsync(
        StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default);

    /// <summary>Projects an immutable exit-stage snapshot inside its source event transaction.</summary>
    /// <param name="transaction">The caller-owned transaction; this method never commits it.</param>
    /// <param name="workflow">The recorded exit-stage facts.</param>
    /// <param name="cancellationToken">Cancels the write attempt.</param>
    /// <returns>A task representing the projection mutation.</returns>
    Task ProjectExitWorkflowAsync(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default);

    /// <summary>Projects an exit-position workflow revision.</summary>
    /// <param name="workflow">The workflow projection to persist.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
    /// <returns>A task representing the asynchronous projection operation.</returns>
    Task ProjectExitWorkflowAsync(
        ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default);
}
