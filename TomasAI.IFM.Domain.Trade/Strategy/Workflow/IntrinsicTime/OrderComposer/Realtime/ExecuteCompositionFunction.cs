using TomasAI.IFM.Shared.EventSourcing;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;

/// <summary>Calls the new Function route only for an accepted, durably frozen request and maps its reply to workflow commands.</summary>
public static class ExecuteCompositionFunction
{
    public static async ValueTask DispatchAsync(this ExecuteOrderCompositionPipelineCommand execute, IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        var read = new RedispatchCurrentStrategyPipelineCommand { EntityId = execute.WorkflowEntityId,
            Subject = new(ActorType.Command, RedispatchCurrentStrategyPipelineCommand.Actor, RedispatchCurrentStrategyPipelineCommand.Verb, execute.WorkflowEntityId.Format()) };
        var current = (await context.WorkflowRepository.LoadStateAsync(read).ConfigureAwait(false)).CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.OrderComposition }
            || current.WorkflowId != execute.WorkflowId || current.WorkflowRevision != execute.InputWorkflowRevision
            || current.CompositionExecution?.InputSha256 != execute.InputSha256) return;
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent> terminal;
        try
        {
            var remaining = execute.ExpiresAtUtc - now;
            using var timeout = new CancellationTokenSource((remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero) + TimeSpan.FromSeconds(5));
            var reply = await context.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>(execute, timeout.Token).ConfigureAwait(false);
            terminal = reply.Value ?? throw new InvalidDataException("Missing composition Function reply.");
            CompositionRulesContract.Require(terminal.IsTerminal, "OC.RESULT.INVALID");
            if (terminal.IsCompleted)
                CompositionRulesContract.Require(terminal.Completed!.Id == execute.CommandId && terminal.Completed.WorkflowId == execute.WorkflowId
                    && terminal.Completed.EntityId == execute.WorkflowEntityId && terminal.Completed.RequestFingerprint == execute.InputSha256
                    && terminal.Completed.InputWorkflowRevision == execute.InputWorkflowRevision, "OC.RESULT.INVALID");
            else CompositionRulesContract.Require(terminal.Failed!.CommandId == execute.CommandId && terminal.Failed.WorkflowId == execute.WorkflowId
                && terminal.Failed.EntityId == execute.WorkflowEntityId && terminal.Failed.InputWorkflowRevision == execute.InputWorkflowRevision,
                "OC.RESULT.INVALID");
        }
        catch (Exception ex)
        {
            if (ex is not CompositionException && context.TimeProvider.GetUtcNow().UtcDateTime < current.ExpiresAtUtc) throw;
            terminal = OrderCompositionFunctionActor.MapEvent(new(typeof(OrderCompositionFunctionFailedEvent), execute,
                Exception: ex is CompositionException ? ex : new CompositionException("OC.TRANSPORT.FAILED")), context.TimeProvider);
        }
        Guid Id(string verb) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{execute.WorkflowId}|{execute.InputWorkflowRevision}|{execute.CommandId}|{verb}")).AsSpan(0, 16));
        if (terminal.IsCompleted)
        {
            var completed = terminal.Completed!;
            var result = OrderCompositionContracts.ReadResult(completed.Result);
            var command = new CompleteOrderCompositionCommand
            {
                CommandId = Id(CompleteOrderCompositionCommand.Verb), Subject = new(ActorType.Command, CompleteOrderCompositionCommand.Actor,
                    CompleteOrderCompositionCommand.Verb, execute.WorkflowEntityId.Format()), EntityId = execute.WorkflowEntityId,
                WorkflowId = execute.WorkflowId, InputWorkflowRevision = execute.InputWorkflowRevision, SourceEventId = completed.Id,
                Result = completed.Result, CorrelationId = execute.CorrelationId, CausationId = completed.Id, CompletedAtUtc = completed.CompletedAtUtc,
                SelectedContracts = result.Candidate is null ? null : new(execute.MarketSnapshot.ScopeId,
                    result.Candidate.Legs.Select(x => x.InstrumentId).Order(StringComparer.Ordinal).ToImmutableArray())
            };
            await context.SendAsync<CompleteOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, command.EntityId).ConfigureAwait(false);
        }
        else
        {
            var failure = terminal.Failed!;
            if (failure.ReasonCode == "OC.TIME.EXPIRED")
            {
                var timeout = new TimeoutOrderCompositionCommand
                {
                    CommandId = Id(TimeoutOrderCompositionCommand.Verb), EntityId = execute.WorkflowEntityId,
                    Subject = new(ActorType.Command, TimeoutOrderCompositionCommand.Actor, TimeoutOrderCompositionCommand.Verb, execute.WorkflowEntityId.Format()),
                    WorkflowId = execute.WorkflowId, ExpectedWorkflowRevision = execute.InputWorkflowRevision,
                    ExpectedStage = StrategyWorkflowStage.OrderComposition, TimeoutId = execute.CommandId, TimedOutAtUtc = failure.ErrorDate
                };
                await context.SendAsync<TimeoutOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(timeout, timeout.EntityId).ConfigureAwait(false);
                return;
            }
            var command = new FailOrderCompositionCommand
            {
                CommandId = Id(FailOrderCompositionCommand.Verb), Subject = new(ActorType.Command, FailOrderCompositionCommand.Actor,
                    FailOrderCompositionCommand.Verb, execute.WorkflowEntityId.Format()), EntityId = execute.WorkflowEntityId,
                WorkflowId = execute.WorkflowId, InputWorkflowRevision = execute.InputWorkflowRevision, SourceEventId = execute.CommandId,
                CorrelationId = execute.CorrelationId, CausationId = execute.CommandId, FailedAtUtc = failure.ErrorDate,
                Failure = new() { ErrorCode = failure.ErrorCode, ErrorMessage = failure.ErrorMessage, ErrorData = failure.ReasonCode,
                    ErrorType = "OrderCompositionFailed", FailedAtUtc = failure.ErrorDate }
            };
            await context.SendAsync<FailOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, command.EntityId).ConfigureAwait(false);
        }
    }
}
