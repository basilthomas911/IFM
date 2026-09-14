using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Position.Workflow.Realtime;

/// <summary>Runs the accepted close handoff after the durable strategy workflow start has committed.</summary>
public static class StrategyExitWorkflowExecution
{
    public static async ValueTask ExecuteAsync(ExitPositionWorkflowStartedEvent started,
        IEventActorContext context, TimeProvider timeProvider,
        ITradePlanDbWriteContext tradePlanDb)
    {
        await tradePlanDb.ProjectExitWorkflowAsync(new ExitPositionWorkflowProjection
        {
            WorkflowId = started.EntityId,
            StrategyKind = started.StrategyKind,
            State = ExitPositionWorkflowState.Started,
            StageRevision = 1,
            UpdatedAtUtc = Utc(started.ReceivedOn),
            SourcePlanEventId = started.SourcePlanEventId,
            ExitPlan = started.ExitPlan
        }).ConfigureAwait(false);

        var (composerActor, riskActor) = started.StrategyKind switch
        {
            TradeStrategyKind.IronCondor => (ExitPositionWorkflowActorNames.IronCondorOrderComposer,
                ExitPositionWorkflowActorNames.IronCondorRiskManager),
            TradeStrategyKind.VerticalSpread => (ExitPositionWorkflowActorNames.VerticalSpreadOrderComposer,
                ExitPositionWorkflowActorNames.VerticalSpreadRiskManager),
            TradeStrategyKind.FuturesOutright => (ExitPositionWorkflowActorNames.FuturesOrderComposer,
                ExitPositionWorkflowActorNames.FuturesRiskManager),
            _ => throw new InvalidOperationException($"EXIT.WORKFLOW.UNSUPPORTED_STRATEGY;{started.StrategyKind}")
        };
        var compose = new ComposeExitOrderCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId(
                $"{started.EntityId.Format()}|{ComposeExitOrderCommand.Verb}"),
            Subject = new(ActorType.Function, composerActor, ComposeExitOrderCommand.Verb,
                started.EntityId.Format()),
            EntityId = started.EntityId,
            StrategyKind = started.StrategyKind,
            Started = started
        };
        compose = compose with { InputHash = TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(new
        {
            compose.EntityId, compose.StrategyKind, compose.Started.SourcePlanEventId,
            Plan = compose.Started.ExitPlan.ContentHash
        }) };
        var composedReply = await context.RequestFunctionAsync<ComposeExitOrderCommand,
            ExitPositionWorkflowId, FunctionResult<ExitOrderCompositionCompletedEvent,
                ExitPositionWorkflowFailedEvent>>(compose).ConfigureAwait(false);
        var composedTerminal = composedReply.Value ?? throw new InvalidOperationException(
            $"EXIT.COMPOSITION.RESULT_MISSING;{composedReply.ErrorCode};{composedReply.ErrorMessage}");
        if (!composedTerminal.IsCompleted)
            throw new InvalidOperationException(
                $"EXIT.COMPOSITION.FAILED;{composedTerminal.Failed!.ErrorData};{composedTerminal.Failed.ErrorMessage}");
        var composed = composedTerminal.Completed!;
        await tradePlanDb.ProjectExitWorkflowAsync(new ExitPositionWorkflowProjection
        {
            WorkflowId = started.EntityId,
            StrategyKind = started.StrategyKind,
            State = ExitPositionWorkflowState.OrderComposed,
            StageRevision = 2,
            UpdatedAtUtc = Utc(composed.ReceivedOn),
            SourcePlanEventId = started.SourcePlanEventId,
            ExitPlan = started.ExitPlan,
            Composition = composed.Composition
        }).ConfigureAwait(false);

        var risk = new EvaluatePositionExitRiskCommand
        {
            CommandId = TradePlanContractIdentity.DeterministicId(
                $"{started.EntityId.Format()}|{EvaluatePositionExitRiskCommand.Verb}"),
            Subject = new(ActorType.Function, riskActor, EvaluatePositionExitRiskCommand.Verb,
                started.EntityId.Format()),
            EntityId = started.EntityId,
            StrategyKind = started.StrategyKind,
            Composition = composed.Composition,
            CompositionEventId = composed.Id
        };
        risk = risk with { InputHash = TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialCanonicalHash.Compute(new
        {
            risk.EntityId, risk.StrategyKind, risk.CompositionEventId,
            risk.Composition.CompositionHash
        }) };
        var riskReply = await context.RequestFunctionAsync<EvaluatePositionExitRiskCommand,
            ExitPositionWorkflowId, FunctionResult<PositionExitRiskCompletedEvent,
                ExitPositionWorkflowFailedEvent>>(risk).ConfigureAwait(false);
        var riskTerminal = riskReply.Value ?? throw new InvalidOperationException(
            $"EXIT.RISK.RESULT_MISSING;{riskReply.ErrorCode};{riskReply.ErrorMessage}");
        if (!riskTerminal.IsCompleted)
            throw new InvalidOperationException(
                $"EXIT.RISK.FAILED;{riskTerminal.Failed!.ErrorData};{riskTerminal.Failed.ErrorMessage}");
        var riskCompleted = riskTerminal.Completed!;
        var decision = riskCompleted.Decision;
        await tradePlanDb.ProjectExitWorkflowAsync(new ExitPositionWorkflowProjection
        {
            WorkflowId = started.EntityId,
            StrategyKind = started.StrategyKind,
            State = decision.ExecuteTradeOrder
                ? ExitPositionWorkflowState.RiskAccepted
                : ExitPositionWorkflowState.NoTradeOrders,
            StageRevision = 3,
            UpdatedAtUtc = Utc(riskCompleted.ReceivedOn),
            SourcePlanEventId = started.SourcePlanEventId,
            ExitPlan = started.ExitPlan,
            Composition = composed.Composition,
            RiskDecision = decision
        }).ConfigureAwait(false);
        if (!decision.ExecuteTradeOrder)
            return;

        await DispatchTradeOrderAsync(decision.TradeOrder!, decision.PortfolioCompletedEventId, context, timeProvider)
            .ConfigureAwait(false);
    }

    static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    static async ValueTask DispatchTradeOrderAsync(TradeOrderDefinition order, Guid portfolioEventId,
        IEventActorContext context, TimeProvider timeProvider)
    {
        var seed = TradeHandoffIdentity.Create("exit-order", portfolioEventId.ToString("N"), order.Id.Format());
        await context.SendAsync<CreateTradeOrderCommand, TradeOrderId>(new()
        {
            CommandId = TradeHandoffIdentity.Create("exit-order-create", seed.ToString("N"), order.Id.Format()),
            Subject = Subject(CreateTradeOrderCommand.Verb), EntityId = order.Id, Order = order
        }, order.Id).ConfigureAwait(false);
        await context.SendAsync<ApproveTradeOrderCommand, TradeOrderId>(new()
        {
            CommandId = TradeHandoffIdentity.Create("exit-order-approve", seed.ToString("N"), order.Id.Format()),
            Subject = Subject(ApproveTradeOrderCommand.Verb), EntityId = order.Id
        }, order.Id).ConfigureAwait(false);
        await context.SendAsync<ReadyTradeOrderCommand, TradeOrderId>(new()
        {
            CommandId = TradeHandoffIdentity.Create("exit-order-ready", seed.ToString("N"), order.Id.Format()),
            Subject = Subject(ReadyTradeOrderCommand.Verb), EntityId = order.Id
        }, order.Id).ConfigureAwait(false);
        var attempt = TradeHandoffIdentity.Create("exit-order-execution", seed.ToString("N"), order.Id.Format());
        await context.SendAsync<BindTradeOrderExecutionCommand, TradeOrderId>(new()
        {
            CommandId = TradeHandoffIdentity.Create("exit-order-bind", seed.ToString("N"), order.Id.Format()),
            Subject = Subject(BindTradeOrderExecutionCommand.Verb), EntityId = order.Id,
            ExecutionAttemptId = attempt, ExecutionChannel = ExecutionChannel.Broker,
            EffectiveAtUtc = timeProvider.GetUtcNow().UtcDateTime
        }, order.Id).ConfigureAwait(false);
        return;

        ActorSubject Subject(string verb) =>
            new(ActorType.Command, TradeOrderActorNames.Command, verb, order.Id.Format());
    }
}
