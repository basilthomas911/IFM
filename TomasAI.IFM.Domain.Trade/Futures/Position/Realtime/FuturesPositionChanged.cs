using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;

public static class FuturesPositionChanged
{
    public static async ValueTask ExecuteAsync(this FuturesPositionChangedEvent changed,
        IFuturesTradePositionRealtimeContext context)
    {
        var valueDate = DateOnly.FromDateTime(changed.State.AsOfUtc);
        var id = new FuturesTradePlanId(changed.State.Id, valueDate);
        var commandId = TradePlanContractIdentity.DeterministicId(
            $"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateFuturesTradePlanCommand.Verb}");
        var command = new UpdateFuturesTradePlanCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor,
                UpdateFuturesTradePlanCommand.Verb, id.Format()),
            EntityId = id,
            Position = changed.State,
            Parameters = context.Parameters,
            SourceEventId = changed.Id,
            RequestedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime
        };
        var reply = await context.RequestFunctionAsync<UpdateFuturesTradePlanCommand,
            FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent,
                TradePlanFailedEvent<FuturesTradePlanId>>>(command).ConfigureAwait(false);
        var terminal = reply.Value ?? throw new InvalidOperationException(
            $"Futures Trade Plan Function returned no result: {reply.ErrorMessage}");
        if (!terminal.IsCompleted)
            throw new InvalidOperationException($"Futures Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
        var completed = terminal.Completed!;
        if (!completed.Plan.RequiresExit || completed.Plan.State != TradePlanState.ExitRequired)
            return;

        var workflowId = new ExitPositionWorkflowId(changed.State.Id, valueDate,
            TradePlanContractIdentity.DeterministicId(
                $"{completed.Id:N}|{completed.Plan.ReasonCode}|FuturesExit"));
        var start = new StartFuturesExitPositionWorkflowCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId(
                $"{workflowId.Format()}|{StartFuturesExitPositionWorkflowCommand.Verb}"),
            Subject = new(ActorType.Command, StartFuturesExitPositionWorkflowCommand.Actor,
                StartFuturesExitPositionWorkflowCommand.Verb, workflowId.Format()),
            EntityId = workflowId,
            ExitPlan = completed
        };
        await context.SendAsync<StartFuturesExitPositionWorkflowCommand,
            ExitPositionWorkflowId>(start, workflowId).ConfigureAwait(false);
    }
}
