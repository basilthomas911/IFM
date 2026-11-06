using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Runs one recoverable handoff for an already committed exit-workflow transition.</summary>
internal static class ExitWorkflowContinuation
{
    internal static async Task ApplyAsync(IEvent source, IActorProducer producer, CancellationToken token)
    {
        switch (source)
        {
            case ExitPositionWorkflowStartedEvent started:
                await ComposeAsync(started, producer, token).ConfigureAwait(false);
                break;
            case ExitPositionWorkflowOrderComposedEvent composed:
                await EvaluateRiskAsync(composed, producer, token).ConfigureAwait(false);
                break;
            case ExitPositionWorkflowRiskRecordedEvent { Snapshot.Projection.State: ExitPositionWorkflowState.RiskAccepted } accepted:
                await DispatchAsync(accepted, producer, token).ConfigureAwait(false);
                break;
        }
    }

    internal static Guid SourceIdentity(IEvent source) => source.Id != Guid.Empty
        ? source.Id
        : source is ExitPositionWorkflowStartedEvent legacy
            ? ExitWorkflowLifecycleIdentity.Transition(legacy.EntityId, ExitPositionWorkflowStartedEvent.Verb, source.CommandId)
            : throw new InvalidDataException("Exit workflow stage has no source identity.");

    static async Task ComposeAsync(ExitPositionWorkflowStartedEvent started, IActorProducer producer, CancellationToken token)
    {
        var actor = ExitWorkflowLifecycleIdentity.Functions(started.StrategyKind).Composer;
        var command = new ComposeExitOrderCommand
        {
            CommandId = ExitWorkflowLifecycleIdentity.Function(started.EntityId, ComposeExitOrderCommand.Verb),
            Subject = new(ActorType.Function, actor, ComposeExitOrderCommand.Verb, started.EntityId.Format()),
            EntityId = started.EntityId, StrategyKind = started.StrategyKind, Started = started,
            InputHash = ExitWorkflowLifecycleIdentity.CompositionFingerprint(started)
        };
        var reply = await producer.RequestFunctionAsync<ComposeExitOrderCommand, ExitPositionWorkflowId,
            FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (reply.Value?.Completed is { } receipt)
            await SendAsync(ForSource(new RecordExitOrderCompositionCommand { Receipt = receipt },
                RecordExitOrderCompositionCommand.Verb, started), producer, token).ConfigureAwait(false);
        else
            await FailOrRetryAsync(started, reply.Value?.Failed, reply.ErrorMessage, producer, token).ConfigureAwait(false);
    }

    static async Task EvaluateRiskAsync(ExitPositionWorkflowOrderComposedEvent composed, IActorProducer producer, CancellationToken token)
    {
        var snapshot = composed.Snapshot;
        var composition = snapshot.Projection.Composition ?? throw new InvalidDataException("Committed exit composition is missing.");
        var actor = ExitWorkflowLifecycleIdentity.Functions(snapshot.Projection.StrategyKind).Risk;
        var command = new EvaluatePositionExitRiskCommand
        {
            CommandId = ExitWorkflowLifecycleIdentity.Function(composed.EntityId, EvaluatePositionExitRiskCommand.Verb),
            Subject = new(ActorType.Function, actor, EvaluatePositionExitRiskCommand.Verb, composed.EntityId.Format()),
            EntityId = composed.EntityId, StrategyKind = snapshot.Projection.StrategyKind, Started = snapshot.Started,
            Composition = composition, CompositionEventId = snapshot.CompositionReceiptId,
            InputHash = ExitWorkflowLifecycleIdentity.RiskFingerprint(composition, snapshot.CompositionReceiptId)
        };
        var reply = await producer.RequestFunctionAsync<EvaluatePositionExitRiskCommand, ExitPositionWorkflowId,
            FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (reply.Value?.Completed is { } receipt)
            await SendAsync(ForSource(new RecordExitRiskDecisionCommand { Receipt = receipt },
                RecordExitRiskDecisionCommand.Verb, composed), producer, token).ConfigureAwait(false);
        else
            await FailOrRetryAsync(composed, reply.Value?.Failed, reply.ErrorMessage, producer, token).ConfigureAwait(false);
    }

    static async Task DispatchAsync(ExitPositionWorkflowRiskRecordedEvent accepted, IActorProducer producer, CancellationToken token)
    {
        var decision = accepted.Snapshot.Projection.RiskDecision ?? throw new InvalidDataException("Committed exit risk is missing.");
        var order = decision.TradeOrder ?? throw new InvalidDataException("Accepted exit risk lacks the authorized Trade Order.");
        Guid Id(string action) => ExitWorkflowLifecycleIdentity.OrderHandoff(decision, action);
        ActorSubject Subject(string verb) => new(ActorType.Command, TradeOrderActorNames.Command, verb, order.Id.Format());
        await SendOrderAsync(new CreateTradeOrderCommand
            { CommandId = Id("create"), Subject = Subject(CreateTradeOrderCommand.Verb), EntityId = order.Id, Order = order }, producer, token).ConfigureAwait(false);
        await SendOrderAsync(new ApproveTradeOrderCommand
            { CommandId = Id("approve"), Subject = Subject(ApproveTradeOrderCommand.Verb), EntityId = order.Id }, producer, token).ConfigureAwait(false);
        await SendOrderAsync(new ReadyTradeOrderCommand
            { CommandId = Id("ready"), Subject = Subject(ReadyTradeOrderCommand.Verb), EntityId = order.Id }, producer, token).ConfigureAwait(false);
        await SendOrderAsync(new BindTradeOrderExecutionCommand
        {
            CommandId = Id("bind"), Subject = Subject(BindTradeOrderExecutionCommand.Verb), EntityId = order.Id,
            ExecutionAttemptId = Id("execution"), ExecutionChannel = ExecutionChannel.Broker,
            EffectiveAtUtc = accepted.Snapshot.Projection.UpdatedAtUtc
        }, producer, token).ConfigureAwait(false);
        await SendAsync(ForSource(new CompleteExitPositionWorkflowCommand
            { ExecutionAttemptId = Id("execution"), BindingCommandId = Id("bind") },
            CompleteExitPositionWorkflowCommand.Verb, accepted), producer, token).ConfigureAwait(false);
    }

    static async Task FailOrRetryAsync(IEvent source, ExitPositionWorkflowFailedEvent? failed, string transportMessage,
        IActorProducer producer, CancellationToken token)
    {
        if (failed is null || !IsBusinessRejection(failed))
            throw new InvalidOperationException($"Exit Function handoff remains unconfirmed: {failed?.ErrorMessage ?? transportMessage}");
        await SendAsync(ForSource(new FailExitPositionWorkflowCommand { ReasonCode = failed.ErrorData, Detail = failed.ErrorMessage },
            FailExitPositionWorkflowCommand.Verb, source), producer, token).ConfigureAwait(false);
    }

    static bool IsBusinessRejection(ExitPositionWorkflowFailedEvent failed) =>
        failed.ErrorData is "EXIT.VALIDATION.FAILED" or "EXIT.PARSING.FAILED" ||
        failed.ErrorMessage.StartsWith("EXIT.RISK.INVALID_COMPOSITION", StringComparison.Ordinal) ||
        failed.ErrorMessage.StartsWith("EXIT.COMPOSITION.INVALID_", StringComparison.Ordinal);

    internal static TCommand ForSource<TCommand>(TCommand request, string verb, IEvent source)
        where TCommand : ExitPositionWorkflowLifecycleCommand
    {
        var projection = ExitWorkflowSnapshotFacts.From(source);
        var predecessor = SourceIdentity(source);
        var entity = projection.WorkflowId;
        return request with
        {
            CommandId = ExitWorkflowLifecycleIdentity.Transition(entity, verb, predecessor),
            Subject = new(ActorType.Command, ExitPositionWorkflowActorNames.Owner(projection.StrategyKind), verb, entity.Format()),
            EntityId = entity, ExpectedRevision = projection.StageRevision, PredecessorEventId = predecessor
        };
    }

    internal static async Task SendAsync<TCommand>(TCommand command, IActorProducer producer, CancellationToken token)
        where TCommand : ExitPositionWorkflowLifecycleCommand
    {
        var result = await producer.RequestAsync<TCommand, ExitPositionWorkflowId, GuidResult>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"{command.CommandName} rejected: {result.ErrorCode};{result.ErrorMessage}");
    }

    static async Task SendOrderAsync<TCommand>(TCommand command, IActorProducer producer, CancellationToken token)
        where TCommand : class, ICommand<TradeOrderId>
    {
        var result = await producer.RequestAsync<TCommand, TradeOrderId, GuidResult>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"{command.CommandName} rejected: {result.ErrorCode};{result.ErrorMessage}");
    }
}
