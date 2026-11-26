using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
/// <summary>Requests a current monitoring calculation when background pricing, reference inputs or freshness change.</summary>
public static class IronCondorMonitoringInputsChanged
{

    extension(IronCondorMonitoringInputsChangedEvent changed)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The exception message is a published domain reason code consumed by failure mapping; adding a parameter suffix would change that contract.")]
        /// <summary>Rejects retired observations before requesting the same source-first trade-plan function as option leg updates.</summary>
        /// <param name = "changed">The coherent captured inputs and latest observed position; not retained plan history.</param>
        /// <param name = "context">The current lifetime's generation-fenced messaging capability.</param>
        /// <returns>The bounded monitoring request. No order or exit workflow is dispatched.</returns>
        public async ValueTask ExecuteAsync(IIronCondorTradePositionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            string? payloadSha256=null;
            if (!context.IsCurrentMonitoringPosition(changed.PositionSnapshot, changed.MonitoringGenerationId))
            {
                if(context is not IIronCondorMonitoringReplayEvidence replay)return;
                payloadSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePack.MessagePackSerializer.Serialize(changed)));
                if(!replay.IsCurrentRecordedMonitoringInput(changed,payloadSha256))return;
            }
            if (changed.EntityId != changed.PositionSnapshot.Id || changed.ValueDate == default || changed.ReceivedOn.Kind != DateTimeKind.Utc)
                throw new ArgumentException("IronCondorMonitoring.INPUTS.IDENTITY_INVALID: matching position, session and UTC capture required.");
            payloadSha256??=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(MessagePack.MessagePackSerializer.Serialize(changed)));
            if(context is IIronCondorMonitoringReplayEvidence evidence)await evidence.RecordMonitoringGenerationAcceptanceAsync(changed,payloadSha256).ConfigureAwait(false);
            await new IronCondorPositionChangedEvent
            {
                Id = changed.Id,
                EntityId = changed.EntityId,
                PositionSnapshot = changed.PositionSnapshot,
                ReceivedOn = changed.ReceivedOn
            }.CalculateMonitoringPlanAsync(context, changed.ValueDate, changed.IronCondorTradePlanInputs, changed.ReceivedOn,
                payloadSha256).ConfigureAwait(false);
        }
    }
}
