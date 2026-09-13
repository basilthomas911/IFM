using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleInjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Trade.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Trade.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.State;
using TomasAI.IFM.Domain.Trade.Futures.Trade.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Trade.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
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
        AssertExactlyOne<IEventSourceActorStateRepository<FuturesTradeCommandState>>(registrations);
        AssertExactlyOne<IEventSourceActorStateRepository<FuturesOptionTradeCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<FuturesPositionCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<IronCondorPositionCommandState>>(registrations);
        AssertExactlyOne<IResidentEventSourceActorStateRepository<VerticalSpreadPositionCommandState>>(registrations);
        AssertExactlyOne<IEventProjector<TradeOrderCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<OrderExecutionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesOptionTradeCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesTradeCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesTradePositionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(registrations);
        AssertExactlyOne<IEventProjector<FuturesVerticalSpreadTradePositionCommandActor>>(registrations);
    }

    static void AssertExactlyOne<T>(IEnumerable<InstanceProducer> registrations) =>
        registrations.Count(registration => registration.ServiceType == typeof(T)).Should().Be(1);
}
