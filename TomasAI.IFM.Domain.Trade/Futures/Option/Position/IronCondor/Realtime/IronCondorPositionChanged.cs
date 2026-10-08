using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;

public static class IronCondorPositionChanged
{
    /// <summary>Captures current inputs immediately and requests the source-first monitoring function for this position update.</summary>
    /// <param name="changed">The committed coherent position observation.</param><param name="context">The generation-fenced monitoring capabilities.</param>
    /// <returns>The monitoring acknowledgement; no order or exit workflow is dispatched.</returns>
    public static async ValueTask ExecuteAsync(this IronCondorPositionChangedEvent changed,
        IIronCondorTradePositionRealtimeContext context)
    {
        if (changed.PositionSnapshot.AsOfUtc.Kind != DateTimeKind.Utc
            || !FuturesTradingValueDate.TryGet(new DateTimeOffset(changed.PositionSnapshot.AsOfUtc), out var valueDate))
            throw new InvalidOperationException("IronCondorTradePlan.VALUE_DATE.UNAVAILABLE: position observation is outside an active futures session.");
        var id = new IronCondorTradePlanId(changed.PositionSnapshot.Id, valueDate);
        var commandId = TradePlanContractIdentity.DeterministicId(
            $"{changed.Id:N}|{changed.PositionSnapshot.PositionSequence}|{UpdateIronCondorTradePlanCommand.Verb}");
        var command = new UpdateIronCondorTradePlanCommand
        {
            CommandId = commandId,
            Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
                UpdateIronCondorTradePlanCommand.Verb, id.Format()),
            EntityId = id,
            Position = changed.PositionSnapshot,
            Parameters = context.Parameters,
            IronCondorTradePlanInputs = context.CaptureMonitoringInputs(changed.PositionSnapshot, valueDate,
                context.TimeProvider.GetUtcNow().UtcDateTime),
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
        // Monitoring records recommendations. Exit execution belongs to its separate workflow design.
    }
}
