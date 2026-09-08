using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;

/// <summary>Executes the frozen selection model and dispatches its outcome through the actor event map.</summary>
public static class ExecuteTradeSelectionPipeline
{
    /// <summary>Checks the deadline, invokes the injected calculation model, and maps the calculated result.</summary>
    public static ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> ExecuteAsync(
        this ExecuteTradeSelectionPipelineCommand c, ITradeSelectionFunctionContext context,
        Func<FunctionEventContext<ExecuteTradeSelectionPipelineCommand>, FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> dispatchEvent,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= c.ExpiresAtUtc) throw new TimeoutException();
        var policy = TradeSelectionContracts.CommonPolicy(c.SelectionBinding);
        TradeSelectionContracts.Require(c.EvaluatedAtUtc <= now.AddSeconds(policy.FutureClockSkewSeconds), "TS.TIME.FUTURE", "Evaluation timestamp is in the future.");
        var result = context.CalculationModel.Calculate(c);
        token.ThrowIfCancellationRequested();
        if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
        return ValueTask.FromResult(dispatchEvent(new(typeof(TradeSelectionFunctionCompletedEvent), c, result)));
    }
}
