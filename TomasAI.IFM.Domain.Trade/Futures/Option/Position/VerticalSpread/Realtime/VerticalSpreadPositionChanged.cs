using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
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

    extension(VerticalSpreadPositionChangedEvent changed)
    {
        public async ValueTask ExecuteAsync(IVerticalSpreadTradePositionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            var valueDate = DateOnly.FromDateTime(changed.State.AsOfUtc);
            var id = new VerticalSpreadTradePlanId(changed.State.Id, valueDate);
            var commandId = TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateVerticalSpreadTradePlanCommand.Verb}");
            var command = new UpdateVerticalSpreadTradePlanCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor, UpdateVerticalSpreadTradePlanCommand.Verb, id.Format()),
                EntityId = id,
                Position = changed.State,
                Parameters = context.Parameters,
                SourceEventId = changed.Id,
                RequestedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime
            };
            if(MessageOrigins.Get(changed) is {} sourceOrigin)
                MessageOrigins.Restore(command,new MessageOrigin(sourceOrigin.OriginatedAtUtc,sourceOrigin.ClockDomainId,sourceOrigin.RunId,sourceOrigin.Generation,changed.Id));
            else MessageOrigins.Capture(command,context.TimeProvider);
            PositionRealtimeIntent? captured=null;
            if(context.PositionIntents is {} intents)
            {
                captured=await intents.CaptureAsync(commandId,PositionRealtimeIntents.Hash(changed.State),()=>new()
                {Kind=PositionIntentKind.VerticalPlan,PositionId=changed.State.Id,RouteGeneration=changed.State.RouteGeneration,
                    PositionSequence=changed.State.PositionSequence,Request=MessagePackSerializer.Serialize(command),Origin=MessageOriginBinary.Serialize(MessageOrigins.Get(command)!)}).ConfigureAwait(false);
                if(captured.IsCompleted)return;
                command=MessagePackSerializer.Deserialize<UpdateVerticalSpreadTradePlanCommand>(captured.Request);
                MessageOrigins.Restore(command,captured.Origin is {} origin ? MessageOriginBinary.Deserialize(origin):null);
                id=command.EntityId;
                if((captured.Flags&PositionIntentFlags.OutputCaptured)!=0)
                {
                    var frozen=MessagePackSerializer.Deserialize<StartVerticalSpreadExitPositionWorkflowCommand>(captured.Output);
                    MessageOrigins.Restore(frozen,captured.OutputOrigin is {} outputOrigin ? MessageOriginBinary.Deserialize(outputOrigin):null);
                    await ConfirmExitAsync(context,frozen).ConfigureAwait(false);
                    await intents.AdvanceAsync(commandId,PositionIntentFlags.Completed|PositionIntentFlags.Published).ConfigureAwait(false);
                    return;
                }
            }
            var reply = await context.RequestFunctionAsync<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanId, FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>>(command).ConfigureAwait(false);
            var terminal = reply.Value ?? throw new InvalidOperationException($"Vertical Spread Trade Plan Function returned no result: {reply.ErrorMessage}");
            if (!terminal.IsCompleted)
                throw new InvalidOperationException($"Vertical Spread Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
            var completed = terminal.Completed!;
            if (!completed.Plan.RequiresExit || completed.Plan.State != TradePlanState.ExitRequired)
            {
                if(context.PositionIntents is {} noExit) await noExit.AdvanceAsync(commandId,PositionIntentFlags.Completed).ConfigureAwait(false);
                return;
            }
            var workflowId = new ExitPositionWorkflowId(changed.State.Id, valueDate, TradePlanContractIdentity.DeterministicId($"{completed.Id:N}|{completed.Plan.ReasonCode}|VerticalSpreadExit"));
            var start = new StartVerticalSpreadExitPositionWorkflowCommand
            {
                CommandId = TradePlanContractIdentity.DeterministicId($"{workflowId.Format()}|{StartVerticalSpreadExitPositionWorkflowCommand.Verb}"),
                Subject = new(ActorType.Command, StartVerticalSpreadExitPositionWorkflowCommand.Actor, StartVerticalSpreadExitPositionWorkflowCommand.Verb, workflowId.Format()),
                EntityId = workflowId,
                ExitPlan = completed
            };
            var requestOrigin=MessageOrigins.Get(command);
            MessageOrigins.Restore(start,requestOrigin is null ? null : new MessageOrigin(requestOrigin.OriginatedAtUtc,requestOrigin.ClockDomainId,requestOrigin.RunId,requestOrigin.Generation,command.CommandId));
            if(context.PositionIntents is {} outputStore)
                await outputStore.AdvanceAsync(commandId,PositionIntentFlags.OutputCaptured,MessagePackSerializer.Serialize(start),outputOrigin:MessageOrigins.Get(start) is {} startOrigin ? MessageOriginBinary.Serialize(startOrigin):null).ConfigureAwait(false);
            await ConfirmExitAsync(context,start).ConfigureAwait(false);
            if(context.PositionIntents is {} completedStore)
                await completedStore.AdvanceAsync(commandId,PositionIntentFlags.Published|PositionIntentFlags.Completed).ConfigureAwait(false);

        }
    }
    static async ValueTask ConfirmExitAsync(IVerticalSpreadTradePositionRealtimeContext context,StartVerticalSpreadExitPositionWorkflowCommand command)
    {
        var accepted=await context.RequestAsync<StartVerticalSpreadExitPositionWorkflowCommand,ExitPositionWorkflowId>(command).ConfigureAwait(false);
        if(!accepted.Success || accepted.Value?.Guid!=command.CommandId)
            throw new InvalidOperationException($"VerticalSpread exit start unconfirmed: {accepted.ErrorMessage}");
    }

}
