using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;

public static class IronCondorPositionChanged
{
    public static async ValueTask ExecuteAsync(this IronCondorPositionChangedEvent changed,
        IIronCondorTradePositionRealtimeContext context)
    {
        var valueDate = DateOnly.FromDateTime(changed.State.AsOfUtc);
        var id = new IronCondorTradePlanId(changed.State.Id, valueDate);
        var commandId = TradePlanContractIdentity.DeterministicId(
            $"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateIronCondorTradePlanCommand.Verb}");
        var command = new UpdateIronCondorTradePlanCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
                UpdateIronCondorTradePlanCommand.Verb, id.Format()),
            EntityId = id,
            Position = changed.State,
            Parameters = context.Parameters,
            SourceEventId = changed.Id,
            RequestedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime
        };
        var reply = await context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand,
            IronCondorTradePlanId, FunctionResult<IronCondorTradePlanUpdatedEvent,
                TradePlanFailedEvent<IronCondorTradePlanId>>>(command).ConfigureAwait(false);
        var terminal = reply.Value ?? throw new InvalidOperationException(
            $"Iron Condor Trade Plan Function returned no result: {reply.ErrorMessage}");
        if (!terminal.IsCompleted)
            throw new InvalidOperationException($"Iron Condor Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
        var completed = terminal.Completed!;
        if (!completed.Plan.RequiresExit || completed.Plan.State != TradePlanState.ExitRequired)
            return;

        var workflowId = new ExitPositionWorkflowId(changed.State.Id, valueDate,
            TradePlanContractIdentity.DeterministicId(
                $"{completed.Id:N}|{completed.Plan.ReasonCode}|IronCondorExit"));
        var start = new StartIronCondorExitPositionWorkflowCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId(
                $"{workflowId.Format()}|{StartIronCondorExitPositionWorkflowCommand.Verb}"),
            Subject = new(ActorType.Command, StartIronCondorExitPositionWorkflowCommand.Actor,
                StartIronCondorExitPositionWorkflowCommand.Verb, workflowId.Format()),
            EntityId = workflowId,
            ExitPlan = completed
        };
        await context.SendAsync<StartIronCondorExitPositionWorkflowCommand,
            ExitPositionWorkflowId>(start, workflowId).ConfigureAwait(false);
    }
}
