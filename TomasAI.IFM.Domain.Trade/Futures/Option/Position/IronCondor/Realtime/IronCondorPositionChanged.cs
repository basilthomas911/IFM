using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
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

    /// <param name="changed">
    /// The committed coherent position observation.
    /// The source observation whose identity is retained by both Function requests.
    /// </param>
    internal static string SourceFingerprint(IronCondorPositionChangedEvent source, DateOnly valueDate) =>
        Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(new object[]
        { source, valueDate })));

    extension(IronCondorPositionChangedEvent changed)
    {
        /// <summary>Captures current inputs immediately and requests the source-first monitoring function for this position update.</summary>
        /// <param name = "context">The generation-fenced monitoring capabilities.</param>
        /// <returns>The monitoring acknowledgement; no order or exit workflow is dispatched.</returns>
        public async ValueTask ExecuteAsync(IIronCondorTradePositionRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(changed);
            ArgumentNullException.ThrowIfNull(context);
            // The durable close coordinator publishes the final position and plan together, then stops the feed.
            if (!changed.PositionSnapshot.IsOpen) return;
            await context.PublishRealtimeAsync(changed.PositionSnapshot).ConfigureAwait(false);
            if (await context.IsClosingAsync(changed.PositionSnapshot.Id).ConfigureAwait(false)) return;
            if (changed.PositionSnapshot.AsOfUtc.Kind != DateTimeKind.Utc || !FuturesTradingValueDate.TryGet(new DateTimeOffset(changed.PositionSnapshot.AsOfUtc), out var valueDate))
                throw new InvalidOperationException("IronCondorTradePlan.VALUE_DATE.UNAVAILABLE: position observation is outside an active futures session.");
            var sourceFingerprint = SourceFingerprint(changed, valueDate);
            var original = context.Requests.Find(changed.Id, sourceFingerprint);
            if (original is not null)
            {
                await changed.RunCapturedPlanAsync(context, original).ConfigureAwait(false);
                return;
            }
            var capturedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime;
            var capturedInputs = context.CaptureMonitoringInputs(changed.PositionSnapshot, valueDate, capturedAtUtc);
            // The input reader publishes a generation-fenced notification when its background load completes.
            // Keep position publication live while reference loading is pending; do not fabricate calendar dates.
            if (capturedInputs is null)
                return;
            await changed.CalculateMonitoringPlanAsync(context, valueDate, capturedInputs, capturedAtUtc).ConfigureAwait(false);
        }

        /// <summary>Runs the same forward-price and trade-plan functions for ticks and completed reference refreshes.</summary>
        ///
        /// <param name="context">The current actor lifetime's messaging capabilities.</param>
        /// <param name="valueDate">The exchange session for this captured observation.</param>
        /// <param name="capturedInputs">The loaded trade references and captured pricing evidence.</param>
        /// <param name="capturedAtUtc">The timestamp shared by the calculation requests.</param>
        /// <returns>The completed forward-price/trade-plan workflow and realtime publication.</returns>
        internal async ValueTask CalculateMonitoringPlanAsync(IIronCondorTradePositionRealtimeContext context, DateOnly valueDate, IronCondorTradePlanInputs capturedInputs, DateTime capturedAtUtc, string? sourceFingerprint = null)
        {
            if (await context.IsClosingAsync(changed.PositionSnapshot.Id).ConfigureAwait(false)) return;
            sourceFingerprint ??= SourceFingerprint(changed, valueDate);
            var existing = context.Requests.Find(changed.Id, sourceFingerprint);
            if (existing is not null)
            {
                await changed.RunCapturedPlanAsync(context, existing).ConfigureAwait(false);
                return;
            }
            var id = new IronCondorTradePlanId(changed.PositionSnapshot.Id, valueDate);
            var commandId = TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{changed.PositionSnapshot.PositionSequence}|{UpdateIronCondorTradePlanCommand.Verb}");
            var forwardRequest = new CalculateIronCondorForwardPriceCommand
            {
                CommandId = TradePlanContractIdentity.DeterministicId($"{changed.Id:N}|{changed.PositionSnapshot.PositionSequence}|{CalculateIronCondorForwardPriceCommand.Verb}"),
                Subject = new(ActorType.Function, CalculateIronCondorForwardPriceCommand.Actor, CalculateIronCondorForwardPriceCommand.Verb, id.Format()),
                EntityId = id,
                Position = changed.PositionSnapshot,
                IronCondorForwardPriceInputs = changed.PositionSnapshot.CaptureForwardPriceInputs(capturedInputs),
                SourceEventId = changed.Id,
                RequestedAtUtc = capturedAtUtc
            };
            var command = new UpdateIronCondorTradePlanCommand
            {
                CommandId = commandId,
                Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor, UpdateIronCondorTradePlanCommand.Verb, id.Format()),
                EntityId = id,
                Position = changed.PositionSnapshot,
                Parameters = context.Parameters,
                IronCondorTradePlanInputs = capturedInputs,
                UseForwardPriceFunction = true,
                SourceEventId = changed.Id,
                RequestedAtUtc = capturedAtUtc
            };
            var capture = context.Requests.Prepare(new()
            {
                SourceFingerprint = sourceFingerprint, Forward = forwardRequest, Plan = command
            });
            await changed.RunCapturedPlanAsync(context, capture).ConfigureAwait(false);
        }

        internal async ValueTask RunCapturedPlanAsync(IIronCondorTradePositionRealtimeContext context, IronCondorPositionRequestReceipt capture)
        {
            if (!capture.PlanFrozen)
            {
                IronCondorForwardPriceCalculatedEvent? forward = null;
                string? forwardFailure = null;
                var forwardRequest = capture.Forward;
                try
                {
                    var response = await context.RequestFunctionAsync<CalculateIronCondorForwardPriceCommand, IronCondorTradePlanId,
                        FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(forwardRequest).ConfigureAwait(false);
                    forward = response.Value?.Completed;
                    forwardFailure = forward is not null ? null
                        : response.Value?.Failed is { } failed ? $"{failed.ErrorData}; {failed.ErrorMessage}"
                        : response.ErrorMessage ?? "Forward price unavailable.";
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    forwardFailure = $"IronCondorForwardPrice.REQUEST.FAILED: {error.Message}";
                }
                capture = context.Requests.FreezePlan(capture, capture.Plan with
                {
                    IronCondorForwardPriceCalculated = forward, ForwardPriceFailure = forwardFailure
                });
            }
            var command = capture.Plan;
            var reply = await context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId, FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(command).ConfigureAwait(false);
            var terminal = reply.Value ?? throw new InvalidOperationException($"Iron Condor Trade Plan Function returned no result: {reply.ErrorMessage}");
            if (terminal.Failed?.ErrorData == SupersededIronCondorPlanObservationException.Reason)
            {
                context.Requests.Complete(capture);
                return;
            }
            if (!terminal.IsCompleted)
                throw new InvalidOperationException($"Iron Condor Trade Plan failed: {terminal.Failed!.ErrorData};{terminal.Failed.ErrorMessage}");
            await context.PublishRealtimeAsync(changed.PositionSnapshot, terminal.Completed!.Plan).ConfigureAwait(false);
            context.Requests.Complete(capture);
            // Monitoring records recommendations. Exit execution belongs to its separate workflow design.
        }
    }
}
