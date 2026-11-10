using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Model;

/// <summary>Prepares immutable opening commands from the committed Risk authorization, never from a UI draft.</summary>
public static class StrategyOpeningIntentFactory
{
    public static IReadOnlyList<BeginStrategyExecutionHandoffCommand> Create(WorkflowStrategyStateUpdatedEvent source, DateTime now)
    {
        var view = source.State;
        if (view.OpeningExecutionRoute is not { Environment: BrokerEnvironment.Emulator } route ||
            view.Status != WorkflowStrategyMachineStatus.Completed || view.Outcome != StrategyWorkflowOutcome.Completed ||
            view.CurrentStage != StrategyWorkflowStage.RiskManagement || view.SelectionBinding?.SchemaVersion != 2 ||
            source.WorkflowId != view.WorkflowId || view.PortfolioRiskDecision is not
                { Status: PortfolioRiskDecisionStatus.ExecuteTradeOrders, FinancialRevision: > 0 } decision ||
            decision.WorkflowId != view.WorkflowId.Value)
            return [];
        var orders = decision.TradeOrders;
        var commands = new List<BeginStrategyExecutionHandoffCommand>();
        foreach (var approved in orders)
        {
            if (approved.Components.Length != 1)
                throw new InvalidOperationException("EXECUTION.STRATEGY_COMPONENT_COUNT_UNSUPPORTED");
            var order = approved with { StrategyWorkflowId = source.WorkflowId.Value,
                SetupTrade = new() { FundId = approved.Id.FundId, OrderId = approved.Id.OrderId, TradeId = approved.Components[0].ReservedTradeId } };
            var execution = StrategyOpeningExecutionIdentity.For(order.Id, source.WorkflowId.Value);
            var id = new OrderExecutionId(order.Id, execution);
            if (order.BrokerEnvironment != BrokerEnvironment.Emulator || order.BrokerAccountAlias != route.AccountAlias ||
                order.PositionType != TradeOrderPositionType.Opening || order.Components.Any(component =>
                    !BrokerOrderRequestMapper.TryCreate(order, execution, component.ComponentId,
                        TradeHandoffIdentity.Create("broker-place-operation", id.Format(), component.ComponentId.ToString("N")), out _, out _)))
                throw new InvalidOperationException("EXECUTION.AUTHORIZED_ORDER.INCOMPLETE");
            var definition = new StrategyExecutionHandoffDefinition
            {
                Id = id, Authorization = new()
                {
                    Id = source.Id, CommandId = source.CommandId, EventId = source.EventId,
                    WorkflowId = source.WorkflowId, WorkflowRevision = source.WorkflowRevision,
                    CorrelationId = view.CorrelationId, UpdatedAtUtc = source.UpdatedAtUtc,
                    StateHash = FinancialCanonicalHash.Compute(view), Route = route,
                    UnderlyingContractId = view.TriggerEvent.EntityId.ContractId
                }, Order = order
            };
            definition = definition with { IntentHash = BeginStrategyExecutionHandoff.Fingerprint(definition) };
            commands.Add(new()
            {
                CommandId = TradeHandoffIdentity.Create("strategy-opening-begin", id.Format()),
                EntityId = id, Subject = new(ActorType.Command, BeginStrategyExecutionHandoffCommand.Actor, BeginStrategyExecutionHandoffCommand.Verb, id.Format()),
                Intent = definition
            });
        }
        return commands;
    }

}
