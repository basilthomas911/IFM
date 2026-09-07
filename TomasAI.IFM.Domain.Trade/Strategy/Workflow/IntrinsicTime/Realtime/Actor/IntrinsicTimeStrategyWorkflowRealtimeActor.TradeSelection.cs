using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Extensions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

public sealed partial class IntrinsicTimeStrategyWorkflowRealtimeActor
{
    static async ValueTask ExecuteSelectionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        if (snapshot.State.CompositionHandoff is { Status: CompositionHandoffStatus.ReservationPending })
        { await ReserveSelectionAsync(context, snapshot).ConfigureAwait(false); return; }
        var execute = snapshot.State.SelectionDispatch ?? throw new InvalidOperationException("Missing durable selector request. Historical unbound selector dispatch is disabled.");
        var clock = RequireEventContext(context).TimeProvider;
        FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent> terminal;
        try
        {
            var remaining = execute.ExpiresAtUtc - clock.GetUtcNow().UtcDateTime;
            using var deadline = new CancellationTokenSource((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + FunctionReplyGrace);
            var reply = await context.RequestFunctionAsync<ExecuteTradeSelectionPipelineCommand, TradeSelectionExecutionId, FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>(execute, deadline.Token).ConfigureAwait(false);
            terminal = reply.Value ?? throw new InvalidOperationException("Selector returned no result.");
            if (!terminal.IsTerminal) throw new InvalidOperationException("Invalid selector result union.");
            if (terminal.IsCompleted)
            {
                var completed = terminal.Completed!;
                TradeSelectionContracts.Require(completed.CommandId == execute.CommandId && completed.WorkflowId == execute.WorkflowId && completed.EntityId == execute.WorkflowEntityId
                    && completed.InputWorkflowRevision == execute.InputWorkflowRevision && completed.RequestFingerprint == execute.Fingerprint(), "TS.RESULT.INVALID", "Function reply identity/fingerprint mismatch.");
            }
            else
            {
                var failed = terminal.Failed!;
                TradeSelectionContracts.Require(failed.CommandId == execute.CommandId && failed.WorkflowId == execute.WorkflowId && failed.EntityId == execute.WorkflowEntityId
                    && failed.InputWorkflowRevision == execute.InputWorkflowRevision && failed.InputPayloadSha256 == execute.SelectionBinding.PayloadSha256, "TS.RESULT.INVALID", "Failed Function reply identity mismatch.");
            }
        }
        catch (Exception ex)
        {
            // An unknown transport outcome is recoverable through the saved dispatch. Do not reject a
            // possibly committed Function result while the workflow can still accept its replay.
            if (ex is not TradeSelectionValidationException && clock.GetUtcNow().UtcDateTime < snapshot.State.ExpiresAtUtc) throw;
            terminal = FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>.Fail(ExecuteTradeSelectionPipeline.CreateFailedEvent(execute,
                ex is TradeSelectionValidationException validation ? validation.ReasonCode : "TS.TRANSPORT.FAILED", clock));
        }
        if (terminal.IsCompleted)
        {
            var completed = terminal.Completed!;
            var c = new CompleteTradeSelectionCommand
            {
                CommandId = DeterministicTerminalCommandId(execute.WorkflowEntityId, execute.WorkflowId, execute.InputWorkflowRevision, completed.Id, CompleteTradeSelectionCommand.Verb),
                Subject = WorkflowSubject(CompleteTradeSelectionCommand.Verb, execute.WorkflowEntityId),
                EntityId = execute.WorkflowEntityId,
                WorkflowId = execute.WorkflowId,
                InputWorkflowRevision = execute.InputWorkflowRevision,
                SourceEventId = completed.Id,
                Result = completed.Result,
                CorrelationId = execute.CorrelationId,
                CausationId = completed.Id,
                CompletedAtUtc = completed.CompletedAtUtc
            };
            await context.SendAsync<CompleteTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(c, c.EntityId).ConfigureAwait(false);
        }
        else
        {
            var failed = terminal.Failed!;
            var c = new FailTradeSelectionCommand
            {
                CommandId = DeterministicTerminalCommandId(execute.WorkflowEntityId, execute.WorkflowId, execute.InputWorkflowRevision, execute.CommandId, FailTradeSelectionCommand.Verb),
                Subject = WorkflowSubject(FailTradeSelectionCommand.Verb, execute.WorkflowEntityId),
                EntityId = execute.WorkflowEntityId,
                WorkflowId = execute.WorkflowId,
                InputWorkflowRevision = execute.InputWorkflowRevision,
                SourceEventId = execute.CommandId,
                CorrelationId = execute.CorrelationId,
                CausationId = execute.CommandId,
                FailedAtUtc = failed.ErrorDate,
                Failure = new() { ErrorCode = failed.ErrorCode, ErrorMessage = failed.ErrorMessage, ErrorType = failed.ReasonCode.StartsWith("TS.TIME", StringComparison.Ordinal) ? "TradeSelectionTimedOut" : "TradeSelectionFailed", ErrorData = failed.ReasonCode, FailedAtUtc = failed.ErrorDate }
            };
            await context.SendAsync<FailTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(c, c.EntityId).ConfigureAwait(false);
        }
    }
    static async ValueTask ReserveSelectionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State; var pending = view.CompositionHandoff!; var services = RequireEventContext(context);
        TradeSelectionTelemetry.ReservationPending(pending.UpdatedAtUtc, services.TimeProvider.GetUtcNow().UtcDateTime);
        var remaining = pending.Request.ExpiresAtUtc - services.TimeProvider.GetUtcNow().UtcDateTime;
        if (remaining <= TimeSpan.Zero)
        {
            await TimeoutReservationAsync(context, snapshot).ConfigureAwait(false);
            await ReconcileStoppedSelectionAsync(context, snapshot).ConfigureAwait(false);
            return;
        }
        // Cancellation bounds this consumer's wait. The remote command may already have committed;
        // retain and observe its reply and always replay the same durable idempotency key.
        var operation = services.PortfolioCommands.ReserveCompositionAsync(pending.Request, view.SelectionBinding!.PortfolioSnapshot);
        ServiceResult<TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundCompositionReservationResult> reply;
        try { reply = await operation.WaitAsync(remaining).ConfigureAwait(false); }
        catch (TimeoutException)
        {
            await TimeoutReservationAsync(context, snapshot).ConfigureAwait(false);
            _ = ObserveLateReservationAsync(context, snapshot, operation);
            return;
        }
        if (!reply.Success || reply.Value is null)
        {
            var c = new FailTradeSelectionCommand
            {
                CommandId = DeterministicTerminalCommandId(view.EntityId, view.WorkflowId, view.WorkflowRevision, pending.SelectionSourceEventId, "ReservationFailed"),
                Subject = WorkflowSubject(FailTradeSelectionCommand.Verb, view.EntityId),
                EntityId = view.EntityId,
                WorkflowId = view.WorkflowId,
                InputWorkflowRevision = view.WorkflowRevision,
                SourceEventId = snapshot.Id,
                CorrelationId = view.CorrelationId,
                CausationId = snapshot.Id,
                FailedAtUtc = services.TimeProvider.GetUtcNow().UtcDateTime,
                Failure = new() { ErrorCode = reply.ErrorCode, ErrorType = "ReservationFailed", ErrorMessage = reply.ErrorMessage, ErrorData = "TS.RESERVATION.FAILED", FailedAtUtc = services.TimeProvider.GetUtcNow().UtcDateTime }
            };
            await context.SendAsync<FailTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(c, c.EntityId).ConfigureAwait(false); return;
        }
        await CompleteReservationAsync(context, snapshot, reply.Value).ConfigureAwait(false);
    }
    static async ValueTask CompleteReservationAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot, TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundCompositionReservationResult reservation)
    {
        var view = snapshot.State; var pending = view.CompositionHandoff!;
        TradeSelectionHandoff.ValidateReservation(pending, reservation);
        var command = new CompleteTradeSelectionReservationCommand
        {
            CommandId = DeterministicTerminalCommandId(view.EntityId, view.WorkflowId, pending.AcceptedSelectionRevision, pending.SelectionSourceEventId, CompleteTradeSelectionReservationCommand.Verb),
            Subject = WorkflowSubject(CompleteTradeSelectionReservationCommand.Verb, view.EntityId),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            InputWorkflowRevision = pending.AcceptedSelectionRevision,
            SourceEventId = pending.SelectionSourceEventId,
            Reservation = reservation,
            ReservationRequestSha256 = pending.ReservationRequestSha256,
            CorrelationId = view.CorrelationId,
            CausationId = pending.SelectionSourceEventId,
            CompletedAtUtc = reservation.CommittedOnUtc
        };
        await context.SendAsync<CompleteTradeSelectionReservationCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, command.EntityId).ConfigureAwait(false);
    }
    static async Task ObserveLateReservationAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot, Task<ServiceResult<TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundCompositionReservationResult>> operation)
    {
        try
        {
            var reply = await operation.ConfigureAwait(false);
            if (reply.Success && reply.Value is not null) await CompleteReservationAsync(context, snapshot, reply.Value).ConfigureAwait(false);
        }
        catch (Exception ex) { RequireEventContext(context).Logger.LogError(ex, "Late selection reservation requires redispatch reconciliation for workflow {WorkflowId}", snapshot.WorkflowId); }
    }
    static async ValueTask TimeoutReservationAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State;
        var id = DeterministicTerminalCommandId(view.EntityId, view.WorkflowId, view.WorkflowRevision, snapshot.Id, TimeoutTradeSelectionCommand.Verb);
        var command = new TimeoutTradeSelectionCommand
        {
            CommandId = id,
            Subject = WorkflowSubject(TimeoutTradeSelectionCommand.Verb, view.EntityId),
            EntityId = view.EntityId,
            WorkflowId = view.WorkflowId,
            ExpectedWorkflowRevision = view.WorkflowRevision,
            TimeoutId = id
        };
        await context.SendAsync<TimeoutTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, command.EntityId).ConfigureAwait(false);
    }
    static async ValueTask ReconcileStoppedSelectionAsync(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context, WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var handoff = snapshot.State.CompositionHandoff; var services = RequireEventContext(context);
        if (handoff is { Status: CompositionHandoffStatus.ReservationPending })
        {
            // Read committed orders first: a stopped workflow must not create new identities.
            var known = await services.PortfolioQueries.GetCompositionByWorkflowAsync(snapshot.WorkflowId.Value).ConfigureAwait(false);
            if (!known.Success) throw new InvalidOperationException(known.ErrorMessage);
            foreach (var item in known.Value ?? [])
            {
                var order = await services.PortfolioQueries.GetOrderAsync(item.OrderId).ConfigureAwait(false);
                if (order.Success && order.Value is { } value && value.TradeSelectionResultId == handoff.Request.TradeSelectionResultId && value.Status is "IdentityReserved" or "TemplateSelected" or "Composing")
                {
                    var expired = await services.PortfolioCommands.ExpireCompositionAsync(new(value.PortfolioId, value.FundId, value.OrderId), value.AggregateVersion, "Selection workflow stopped.").ConfigureAwait(false);
                    if (!expired.Success) throw new InvalidOperationException(expired.ErrorMessage);
                }
            }
        }
        if (handoff is { Reservation: { } reservation } && reservation.Order.Status is "IdentityReserved" or "TemplateSelected" or "Composing")
        {
            var order = await services.PortfolioQueries.GetOrderAsync(reservation.Order.OrderId).ConfigureAwait(false);
            if (!order.Success || order.Value is null) throw new InvalidOperationException(order.ErrorMessage);
            if (order.Value.Status is "IdentityReserved" or "TemplateSelected" or "Composing")
            {
                var result = await services.PortfolioCommands.ExpireCompositionAsync(new(order.Value.PortfolioId, order.Value.FundId, order.Value.OrderId), order.Value.AggregateVersion, "Selection workflow stopped.").ConfigureAwait(false);
                if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
            }
        }
    }
}
