using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function;

/// <summary>Executes the frozen composition model and dispatches its outcome through the actor event map.</summary>
public static class ExecuteRiskManagementPipeline
{
    /// <summary>Checks the deadline, invokes the injected calculation model, and maps the calculated result.</summary>
    public static ValueTask<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>> ExecuteAsync(
        this ExecuteRiskManagementPipelineCommand c, IRiskManagementFunctionContext context,
        Func<FunctionEventContext<ExecuteRiskManagementPipelineCommand>, FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>> dispatchEvent,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= c.ExpiresAtUtc) throw new TimeoutException();
        var result = context.CalculationModel.Calculate(c, token);
        token.ThrowIfCancellationRequested();
        if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
        return ValueTask.FromResult(dispatchEvent(new(typeof(RiskManagementFunctionCompletedEvent), c, result)));
    }
}
