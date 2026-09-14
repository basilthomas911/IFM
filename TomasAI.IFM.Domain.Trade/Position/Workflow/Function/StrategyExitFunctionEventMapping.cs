using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Function;

public static class StrategyExitFunctionEventMapping
{
    public static void ValidateComposition(
        ComposeExitOrderCommand request,
        string actorName,
        TradeStrategyKind strategyKind)
    {
        if (request.CommandId == Guid.Empty || !request.EntityId.IsValid ||
            request.StrategyKind != strategyKind || request.Started.EntityId != request.EntityId ||
            request.Started.StrategyKind != strategyKind || request.InputHash.Length != 64 ||
            request.Subject != new ActorSubject(ActorType.Function, actorName,
                ComposeExitOrderCommand.Verb, request.EntityId.Format()))
            throw new ArgumentException("Valid strategy-specific exit-composition routing is required.");
    }

    public static FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
        CompleteComposition(
            FunctionEventContext<ComposeExitOrderCommand> input,
            string actorName,
            TimeProvider clock)
    {
        var request = input.Request ??
            throw new ArgumentException("Exit-composition completion requires its command.");
        if (input.Outcome is ExitOrderCompositionCompletedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<ExitOrderCompositionCompletedEvent,
                ExitPositionWorkflowFailedEvent>.Complete(committed);

        var composition = input.Outcome as ExitOrderComposition ??
            throw new ArgumentException("Exit composition outcome is required.");
        return FunctionResult<ExitOrderCompositionCompletedEvent,
            ExitPositionWorkflowFailedEvent>.Complete(new ExitOrderCompositionCompletedEvent
        {
            Subject = new(ActorType.Function, actorName, ExitOrderCompositionCompletedEvent.Verb,
                request.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId(
                $"{request.CommandId:N}|{ExitOrderCompositionCompletedEvent.Verb}"),
            EntityId = request.EntityId,
            CommandId = request.CommandId,
            AggregateId = request.EntityId.Format(),
            EventSource = actorName,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            Composition = composition,
            RequestFingerprint = request.InputHash
        });
    }

    public static FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>
        FailComposition(
            FunctionEventContext<ComposeExitOrderCommand> input,
            string actorName,
            TimeProvider clock) =>
        FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>.Fail(
            Failure(input.Request, input.Exception, input.Stage, actorName, clock, 27210));

    public static void ValidateRisk(
        EvaluatePositionExitRiskCommand request,
        string actorName,
        TradeStrategyKind strategyKind)
    {
        if (request.CommandId == Guid.Empty || !request.EntityId.IsValid ||
            request.StrategyKind != strategyKind || request.Composition.WorkflowId != request.EntityId ||
            request.Composition.StrategyKind != strategyKind ||
            request.Composition.PositionType != TradeOrderPositionType.Closing ||
            request.InputHash.Length != 64 ||
            request.Subject != new ActorSubject(ActorType.Function, actorName,
                EvaluatePositionExitRiskCommand.Verb, request.EntityId.Format()))
            throw new ArgumentException("Valid strategy-specific position-exit risk routing is required.");
    }

    public static FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
        CompleteRisk(
            FunctionEventContext<EvaluatePositionExitRiskCommand> input,
            string actorName,
            TimeProvider clock)
    {
        var request = input.Request ??
            throw new ArgumentException("Position exit-risk completion requires its command.");
        if (input.Outcome is PositionExitRiskCompletedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<PositionExitRiskCompletedEvent,
                ExitPositionWorkflowFailedEvent>.Complete(committed);

        var decision = input.Outcome as PortfolioCloseRiskDecision ??
            throw new ArgumentException("Position exit-risk decision is required.");
        return FunctionResult<PositionExitRiskCompletedEvent,
            ExitPositionWorkflowFailedEvent>.Complete(new PositionExitRiskCompletedEvent
        {
            Subject = new(ActorType.Function, actorName, PositionExitRiskCompletedEvent.Verb,
                request.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId(
                $"{request.CommandId:N}|{PositionExitRiskCompletedEvent.Verb}"),
            EntityId = request.EntityId,
            CommandId = request.CommandId,
            AggregateId = request.EntityId.Format(),
            EventSource = actorName,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            Decision = decision,
            RequestFingerprint = request.InputHash
        });
    }

    public static FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>
        FailRisk(
            FunctionEventContext<EvaluatePositionExitRiskCommand> input,
            string actorName,
            TimeProvider clock) =>
        FunctionResult<PositionExitRiskCompletedEvent, ExitPositionWorkflowFailedEvent>.Fail(
            Failure(input.Request, input.Exception, input.Stage, actorName, clock, 27220));

    static ExitPositionWorkflowFailedEvent Failure(
        ICommand<ExitPositionWorkflowId>? request,
        Exception? exception,
        FunctionFailureStage stage,
        string actorName,
        TimeProvider clock,
        int fallbackErrorCode)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new ExitPositionWorkflowFailedEvent
        {
            Subject = request?.Subject ?? ActorSubject.Unknown,
            Id = Guid.CreateVersion7(clock.GetUtcNow()),
            EntityId = request?.EntityId ?? default,
            CommandId = request?.CommandId ?? Guid.Empty,
            AggregateId = request?.EntityId.Format() ?? string.Empty,
            EventSource = actorName,
            ReceivedOn = now,
            ErrorDate = now,
            ErrorCode = request?.ErrorCode ?? fallbackErrorCode,
            ErrorMessage = exception?.Message ??
                "Exit workflow request conflicts with committed state.",
            ErrorData = $"EXIT.{stage.ToString().ToUpperInvariant()}.FAILED",
            CommandName = request?.CommandName ?? actorName
        };
    }
}
