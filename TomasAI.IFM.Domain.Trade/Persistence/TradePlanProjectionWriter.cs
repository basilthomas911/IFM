using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Query;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Qualifies plan projection writes and owns the policy for disposable monitoring snapshots.</summary>
public static partial class TradePlanProjectionWriter
{
    /// <summary>Writes the prepared material projection on the accepted event's transaction.</summary>
    public static Task WriteAcceptedAsync(ITradePlanDbWriteContext store, IEnlistedPostgresTransaction transaction,
        StrategyTradePlanSnapshot plan, CancellationToken token = default) =>
        ShouldProject(plan) ? store.ProjectMaterialAsync(transaction, plan, token) : Task.CompletedTask;

    /// <summary>Persists a disposable monitoring snapshot and reports failures without scheduling replay.</summary>
    public static async Task TryWriteDisposableAsync(ITradePlanDbWriteContext store, StrategyTradePlanSnapshot plan,
        ILogger logger, CancellationToken token = default)
    {
        if (!ShouldProject(plan)) return;
        try
        {
            await store.ProjectMaterialAsync(plan, token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            PlanDropped(logger, error, nameof(ITradePlanDbWriteContext.ProjectMaterialAsync), plan.Position.Id,
                plan.PlanRevision, plan.ValueDate);
        }
    }

    /// <summary>Qualifies a recorded exit stage before handing it to the typed persistence operation.</summary>
    public static ExitPositionWorkflowProjection PrepareExitStage(ExitPositionWorkflowProjection workflow)
    {
        ExitWorkflowProjectionFacts.RequireValid(workflow);
        return workflow;
    }

    static bool ShouldProject(StrategyTradePlanSnapshot plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        TradePlanRequestQualification.RequireScope(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate);
        return plan.MaterialChange || plan.Position.StrategyKind == TradeStrategyKind.IronCondor;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{MethodName}: dropping trade plan after persistence failure. PositionId={PositionId} PlanRevision={PlanRevision} ValueDate={ValueDate}")]
    private static partial void PlanDropped(ILogger logger, Exception error, string methodName, object positionId,
        long planRevision, DateOnly valueDate);
}
