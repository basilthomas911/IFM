using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;

public static class DailyFuturesExitDispatch
{
    public static async ValueTask ResumeAsync(IFuturesTradePositionRealtimeContext context, DailyExitIntent intent)
    {
        if (intent.Stage != DailyExitIntentStage.Pending)
        { context.ExitIntents.Finish(intent, intent.Stage, intent.Reason); return; }
        var reply = await context.RequestAsync<StartFuturesExitPositionWorkflowCommand, ExitPositionWorkflowId>(intent.Command).ConfigureAwait(false);
        if (!reply.Success || reply.Value?.Guid != intent.Command.CommandId)
            throw new InvalidOperationException($"Daily exit start unconfirmed: {reply.ErrorMessage}");
        context.ExitIntents.Finish(intent, DailyExitIntentStage.Confirmed);
    }

    public static async ValueTask RecoverAsync(IFuturesTradePositionRealtimeContext context)
    {
        foreach (var intent in context.ExitIntents.Pending())
        {
            if (intent.Stage != DailyExitIntentStage.Pending)
            { context.ExitIntents.Finish(intent, intent.Stage, intent.Reason); continue; }
            var captured = intent.Command.ExitPlan.Plan.Position;
            var current = await context.ReadDailyExitRecoveryPositionAsync(captured.Id).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Daily exit recovery position unavailable; intent remains pending.");
            if (current.Id != captured.Id || current.RouteGeneration < captured.RouteGeneration)
                throw new InvalidOperationException("Daily exit recovery position is inconsistent or behind the captured generation.");
            if (!current.IsOpen || current.RouteGeneration > captured.RouteGeneration)
            { context.ExitIntents.Finish(intent, DailyExitIntentStage.Retired, current.IsOpen ? "generation-superseded" : "position-closed"); continue; }
            if (current.DailyFuturesEntry?.DcTriggerId != captured.DailyFuturesEntry!.DcTriggerId)
                throw new InvalidOperationException("Daily exit recovery entry identity conflicts with the captured intent.");
            await ResumeAsync(context, intent).ConfigureAwait(false);
        }
    }
}
