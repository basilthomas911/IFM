using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
/// <summary>Requests a current monitoring calculation when background pricing, reference inputs or freshness change.</summary>
public static class IronCondorMonitoringInputsChanged
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The exception message is a published domain reason code consumed by failure mapping; adding a parameter suffix would change that contract.")]
    /// <summary>Rejects retired observations before requesting the same source-first trade-plan function as option leg updates.</summary>
    /// <param name = "changed">The coherent captured inputs and latest observed position; not retained plan history.</param>
    /// <param name = "context">The current lifetime's generation-fenced messaging capability.</param>
    /// <returns>The bounded monitoring request. No order or exit workflow is dispatched.</returns>
    public static async ValueTask ExecuteAsync(this IronCondorMonitoringInputsChangedEvent changed, IIronCondorTradePositionRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(context);
        if (!context.IsCurrentMonitoringPosition(changed.PositionSnapshot, changed.MonitoringGenerationId))
            return;
        if (changed.EntityId != changed.PositionSnapshot.Id || changed.ValueDate == default || changed.ReceivedOn.Kind != DateTimeKind.Utc)
            throw new ArgumentException("IronCondorMonitoring.INPUTS.IDENTITY_INVALID: matching position, session and UTC capture required.");
        var id = new IronCondorTradePlanId(changed.EntityId, changed.ValueDate);
        var command = new UpdateIronCondorTradePlanCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{UpdateIronCondorTradePlanCommand.Verb}"),
            Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor, UpdateIronCondorTradePlanCommand.Verb, id.Format()),
            EntityId = id,
            Position = changed.PositionSnapshot,
            Parameters = context.Parameters,
            IronCondorTradePlanInputs = changed.IronCondorTradePlanInputs,
            SourceEventId = changed.Id,
            RequestedAtUtc = changed.ReceivedOn
        };
        var reply = await context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId, FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(command).ConfigureAwait(false);
        if (reply.Value is not { IsCompleted: true })
            throw new InvalidOperationException($"IronCondorMonitoring.EVALUATION.FAILED: {reply.ErrorMessage};{reply.Value?.Failed?.ErrorMessage}");
        await context.PublishRealtimeAsync(changed.PositionSnapshot, reply.Value.Completed!.Plan).ConfigureAwait(false);
    }
}
