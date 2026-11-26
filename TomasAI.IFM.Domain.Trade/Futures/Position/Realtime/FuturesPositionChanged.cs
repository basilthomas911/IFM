using MessagePack;
using TomasAI.IFM.Domain.Trade.Persistence.Realtime;
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

    extension(TomasAI.IFM.Domain.Trade.Shared.Trade.Position.PositionChangedEvent changed)
    {
        public async ValueTask ExecuteAsync(IFuturesTradePositionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            if (changed.State.IsOpen && changed.State.DailyFuturesEntry is not null
                && context.ExitIntents.Find(changed.State) is { } intent)
            {
                await DailyFuturesExitDispatch.ResumeAsync(context, intent).ConfigureAwait(false);
                if(context.PositionIntents is {} completedIntents)
                {
                    var pendingKey=TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateFuturesTradePlanCommand.Verb}");
                    if(await completedIntents.FindAsync(pendingKey,PositionRealtimeIntents.Hash(changed.State)).ConfigureAwait(false) is not null)
                        await completedIntents.AdvanceAsync(pendingKey,PositionIntentFlags.Published|PositionIntentFlags.Completed).ConfigureAwait(false);
                }
                return;
            }
            var valueDate = DateOnly.FromDateTime(changed.State.AsOfUtc);
            var id = new FuturesTradePlanId(changed.State.Id, valueDate);
            var commandId = TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{changed.State.PositionSequence}|{UpdateFuturesTradePlanCommand.Verb}");
            var command = new UpdateFuturesTradePlanCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor, UpdateFuturesTradePlanCommand.Verb, id.Format()),
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
                {Kind=PositionIntentKind.FuturesPlan,PositionId=changed.State.Id,RouteGeneration=changed.State.RouteGeneration,
                    PositionSequence=changed.State.PositionSequence,Request=MessagePackSerializer.Serialize(command),Origin=MessageOriginBinary.Serialize(MessageOrigins.Get(command)!)}).ConfigureAwait(false);
                if(captured.IsCompleted)return;
                command=MessagePackSerializer.Deserialize<UpdateFuturesTradePlanCommand>(captured.Request);
                MessageOrigins.Restore(command,captured.Origin is {} origin ? MessageOriginBinary.Deserialize(origin):null);
                id=command.EntityId;
                if((captured.Flags&PositionIntentFlags.OutputCaptured)!=0)
                {
                    var frozen=MessagePackSerializer.Deserialize<StartFuturesExitPositionWorkflowCommand>(captured.Output);
                    MessageOrigins.Restore(frozen,captured.OutputOrigin is {} outputOrigin ? MessageOriginBinary.Deserialize(outputOrigin):null);
                    if(command.Position.DailyFuturesEntry is not null)
                        await DailyFuturesExitDispatch.ResumeAsync(context,context.ExitIntents.Claim(frozen)).ConfigureAwait(false);
                    else await ConfirmExitAsync(context,frozen).ConfigureAwait(false);
                    await intents.AdvanceAsync(commandId,PositionIntentFlags.Completed|PositionIntentFlags.Published).ConfigureAwait(false);
                    return;
                }
            }
            var reply = await context.RequestFunctionAsync<UpdateFuturesTradePlanCommand, FuturesTradePlanId, FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>>(command).ConfigureAwait(false);
            var terminal = reply.Value ?? throw new InvalidOperationException($"Futures Trade Plan Function returned no result: {reply.ErrorMessage}");
            if (!terminal.IsCompleted)
                throw new InvalidOperationException($"Futures Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
            var completed = terminal.Completed!;
            if (!completed.Plan.RequiresExit || completed.Plan.State != TradePlanState.ExitRequired)
            {
                if(context.PositionIntents is {} noExit) await noExit.AdvanceAsync(commandId,PositionIntentFlags.Completed).ConfigureAwait(false);
                return;
            }
            var workflowId = new ExitPositionWorkflowId(changed.State.Id, valueDate, TradePlanContractIdentity.DeterministicId(
                changed.State.DailyFuturesEntry is null ? $"{completed.Id:N}|{completed.Plan.ReasonCode}|FuturesExit"
                    : $"{changed.State.Id.Format()}|{changed.State.RouteGeneration}|DailyFuturesExit"));
            var start = new StartFuturesExitPositionWorkflowCommand
            {
                CommandId = TradePlanContractIdentity.DeterministicId($"{workflowId.Format()}|{StartFuturesExitPositionWorkflowCommand.Verb}"),
                Subject = new(ActorType.Command, StartFuturesExitPositionWorkflowCommand.Actor, StartFuturesExitPositionWorkflowCommand.Verb, workflowId.Format()),
                EntityId = workflowId,
                ExitPlan = completed
            };
            var requestOrigin=MessageOrigins.Get(command);
            MessageOrigins.Restore(start,requestOrigin is null ? null : new MessageOrigin(requestOrigin.OriginatedAtUtc,requestOrigin.ClockDomainId,requestOrigin.RunId,requestOrigin.Generation,command.CommandId));
            if(context.PositionIntents is {} outputStore)
                await outputStore.AdvanceAsync(commandId,PositionIntentFlags.OutputCaptured,MessagePackSerializer.Serialize(start),outputOrigin:MessageOrigins.Get(start) is {} startOrigin ? MessageOriginBinary.Serialize(startOrigin):null).ConfigureAwait(false);
            if (changed.State.DailyFuturesEntry is not null)
            {
                MessageOrigins.Capture(start, context.TimeProvider);
                var claimed = context.ExitIntents.Claim(start);
                await DailyFuturesExitDispatch.ResumeAsync(context, claimed).ConfigureAwait(false);
            }
            else await ConfirmExitAsync(context,start).ConfigureAwait(false);
            if(context.PositionIntents is {} completedStore)
                await completedStore.AdvanceAsync(commandId,PositionIntentFlags.Published|PositionIntentFlags.Completed).ConfigureAwait(false);

        }
    }
    static async ValueTask ConfirmExitAsync(IFuturesTradePositionRealtimeContext context,StartFuturesExitPositionWorkflowCommand command)
    {
        var accepted=await context.RequestAsync<StartFuturesExitPositionWorkflowCommand,ExitPositionWorkflowId>(command).ConfigureAwait(false);
        if(!accepted.Success || accepted.Value?.Guid!=command.CommandId)
            throw new InvalidOperationException($"Futures exit start unconfirmed: {accepted.ErrorMessage}");
    }

}
