using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command;

/// <summary>Requests broker evidence without sending a placement, replacement or cancellation.</summary>
public static class ReconcileBrokerOrder
{
    extension(ReconcileBrokerOrderCommand command)
    {
        /// <summary>Looks up the exact order and routes recovered facts through the observation pipeline.</summary>
        public async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(IBrokerOrderCommandContext context, BrokerOrderCommandState state)
        {
            if (state.BrokerOrderDefinition is not { } order || order.Id != command.EntityId)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "BrokerOrder.NOT_FOUND");

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var facts = await context.ObservationBridge.ReconcileAsync(order, deadline.Token).ConfigureAwait(false);
            if (facts.Length == 0)
                return new ServiceFailed<GuidResult>(command.ErrorCode, "BrokerOrder.OUTCOME_UNKNOWN: broker returned no matching evidence.");

            // The observation can already be committed even if the original execution handoff failed.
            // Let normal observation processing run first whenever the broker supplies new evidence.
            if (order.Status == BrokerOrderStatus.Cancelled && order.Order.Components.Length == 1 &&
                facts.LastOrDefault(static fact => fact.Kind == BrokerObservationKind.Cancelled && fact.ExecutionFinal) is { } cancelled &&
                facts.All(fact => state.TryGetObservationHash(fact.ObservationId, out var hash) &&
                    string.Equals(hash, BrokerOrderObservationBridge.ContentHash(fact), StringComparison.Ordinal)))
            {
                var executionId = order.Id.Execution;
                var execution = await context.DbFactory.TradeDb.GetOrderExecutionAsync(executionId.TradeOrder,
                    executionId.ExecutionAttemptId, deadline.Token).ConfigureAwait(false);
                if (execution?.Status == OrderExecutionStatus.PartiallyFilled)
                {
                    var retry = BrokerOrderEventProjector.CreateCancellationHandoff(executionId,
                        cancelled.ObservationId, cancelled.OccurredAtUtc, recovery: true);
                    var result = await context.ActorService.RequestAsync<CancelOrderExecutionCommand, OrderExecutionId>(
                        retry, deadline.Token).ConfigureAwait(false);
                    if (!result.Success)
                        return new ServiceFailed<GuidResult>(command.ErrorCode, result.ErrorMessage);
                }
            }
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }
    }
}
