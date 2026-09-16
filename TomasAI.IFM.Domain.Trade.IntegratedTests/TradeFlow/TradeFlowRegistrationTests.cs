using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleInjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.RiskManager.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Function.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

public sealed class TradeFlowRegistrationTests
{
    [Fact]
    public void Startup_discovers_every_trade_flow_actor_repository_and_durable_projector_once()
    {
        using var container = new Container();
        container.Options.EnableAutoVerification = false;
        typeof(global::TomasAI.IFM.Application.Actor.IntegrationTests.Startup)
            .GetMethod("RegisterGenericTypes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [container, new ConfigurationManager(), NullLogger.Instance]);

        var registrations = container.GetCurrentRegistrations();
        AssertExactlyOne<IEventSourceActorStateRepository<TradeOrderCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<OrderExecutionCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<BrokerOrderCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<FuturesTradeCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<FuturesOptionTradeCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<FuturesPositionCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<VerticalSpreadPositionCommandState>>(registrations);
        AssertExactlyOne<IEventProjector<TradeOrderCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<OrderExecutionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<BrokerOrderCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesOptionTradeCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesTradeCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesTradePositionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesVerticalSpreadTradePositionCommandActor>>(registrations);
        AssertExactlyOne<IEventSourceFunctionStateRepository<IronCondorTradePlanFunctionState,
            UpdateIronCondorTradePlanCommand>>(registrations);
        AssertExactlyOne<IEventSourceFunctionStateRepository<VerticalSpreadTradePlanFunctionState,
            UpdateVerticalSpreadTradePlanCommand>>(registrations);
        AssertExactlyOne<IEventSourceFunctionStateRepository<FuturesTradePlanFunctionState,
            UpdateFuturesTradePlanCommand>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<IronCondorExitPositionWorkflowCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<VerticalSpreadExitPositionWorkflowCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<FuturesExitPositionWorkflowCommandState>>(registrations);
        AssertExactlyOne<IEventProjector<IronCondorExitPositionWorkflowCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<VerticalSpreadExitPositionWorkflowCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesExitPositionWorkflowCommandActor>>(registrations);
        AssertExactlyOne<IEventSourceFunctionStateRepository<ExitOrderCompositionFunctionState,
            ComposeExitOrderCommand>>(registrations);
        AssertExactlyOne<IEventSourceFunctionStateRepository<PositionExitRiskFunctionState,
            EvaluatePositionExitRiskCommand>>(registrations);
        AssertExactlyOne<IFunctionActorContext<IronCondorExitOrderCompositionFunctionActor>>(registrations);
        AssertExactlyOne<IFunctionActorContext<IronCondorPositionExitRiskFunctionActor>>(registrations);
        AssertExactlyOne<IFunctionActorContext<VerticalSpreadExitOrderCompositionFunctionActor>>(registrations);
        AssertExactlyOne<IFunctionActorContext<VerticalSpreadPositionExitRiskFunctionActor>>(registrations);
        AssertExactlyOne<IFunctionActorContext<FuturesExitOrderCompositionFunctionActor>>(registrations);
        AssertExactlyOne<IFunctionActorContext<FuturesPositionExitRiskFunctionActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<IronCondorTradePlanQueryActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<VerticalSpreadTradePlanQueryActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<FuturesTradePlanQueryActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<StrategyTradePlanActivityQueryActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<PositionExitWorkflowQueryActor>>(registrations);
        AssertExactlyOne<IQueryActorContext<FuturesOptionTradeQueryActor>>(registrations);
        AssertExactlyOne<IEventActorContext<FuturesOptionTradeEventActor>>(registrations);
    }

    static void AssertExactlyOne<T>(IEnumerable<InstanceProducer> registrations) =>
        registrations.Count(registration => registration.ServiceType == typeof(T)).Should().Be(1);
}
