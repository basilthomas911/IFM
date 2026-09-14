using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime;

public static class VerticalSpreadPositionChanged
{
    public static async ValueTask ExecuteAsync(this VerticalSpreadPositionChangedEvent changed,
        IVerticalSpreadTradePositionRealtimeContext context)
    {
        var valueDate = DateOnly.FromDateTime(changed.State.AsOfUtc);
        var id = new VerticalSpreadTradePlanId(changed.State.Id, valueDate);
        var commandId = TradePlanContractIdentity.DeterministicId(
            $"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateVerticalSpreadTradePlanCommand.Verb}");
        var command = new UpdateVerticalSpreadTradePlanCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor,
                UpdateVerticalSpreadTradePlanCommand.Verb, id.Format()),
            EntityId = id,
            Position = changed.State,
            Parameters = context.Parameters,
            SourceEventId = changed.Id,
            RequestedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime
        };
        var reply = await context.RequestFunctionAsync<UpdateVerticalSpreadTradePlanCommand,
            VerticalSpreadTradePlanId, FunctionResult<VerticalSpreadTradePlanUpdatedEvent,
                TradePlanFailedEvent<VerticalSpreadTradePlanId>>>(command).ConfigureAwait(false);
        var terminal = reply.Value ?? throw new InvalidOperationException(
            $"Vertical Spread Trade Plan Function returned no result: {reply.ErrorMessage}");
        if (!terminal.IsCompleted)
            throw new InvalidOperationException($"Vertical Spread Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
        var completed = terminal.Completed!;
        if (!completed.Plan.RequiresExit || completed.Plan.State != TradePlanState.ExitRequired)
            return;

        var workflowId = new ExitPositionWorkflowId(changed.State.Id, valueDate,
            TradePlanContractIdentity.DeterministicId(
                $"{completed.Id:N}|{completed.Plan.ReasonCode}|VerticalSpreadExit"));
        var start = new StartVerticalSpreadExitPositionWorkflowCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId(
                $"{workflowId.Format()}|{StartVerticalSpreadExitPositionWorkflowCommand.Verb}"),
            Subject = new(ActorType.Command, StartVerticalSpreadExitPositionWorkflowCommand.Actor,
                StartVerticalSpreadExitPositionWorkflowCommand.Verb, workflowId.Format()),
            EntityId = workflowId,
            ExitPlan = completed
        };
        await context.SendAsync<StartVerticalSpreadExitPositionWorkflowCommand,
            ExitPositionWorkflowId>(start, workflowId).ConfigureAwait(false);
    }
}
