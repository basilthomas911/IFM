using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;

/// <summary>Executes the frozen composition model and dispatches its outcome through the actor event map.</summary>
public static class ExecuteOrderCompositionPipeline
{
    /// <summary>Checks the deadline, invokes the injected calculation model, and maps the calculated result.</summary>
    public static ValueTask<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>> ExecuteAsync(
        this ExecuteOrderCompositionPipelineCommand c, IOrderCompositionFunctionContext context,
        Func<FunctionEventContext<ExecuteOrderCompositionPipelineCommand>, FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>> dispatchEvent,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= c.ExpiresAtUtc) throw new TimeoutException();
        var result = context.CalculationModel.Calculate(c, token);
        token.ThrowIfCancellationRequested();
        if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
        return ValueTask.FromResult(dispatchEvent(new(typeof(OrderCompositionFunctionCompletedEvent), c, result)));
    }
}
