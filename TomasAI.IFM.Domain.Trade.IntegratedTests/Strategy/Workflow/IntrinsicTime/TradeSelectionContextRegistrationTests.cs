using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimpleInjector;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed class TradeSelectionContextRegistrationTests
{
    [Fact]
    public void Startup_resolves_actor_with_the_same_domain_and_framework_context_singleton()
    {
        using var container = new Container();
        // Resolve just this actor graph; unrelated host registrations require live infrastructure.
        container.Options.EnableAutoVerification = false;
        typeof(global::TomasAI.IFM.Application.Actor.IntegrationTests.Startup)
            .GetMethod("RegisterGenericTypes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [container, new ConfigurationManager(), NullLogger.Instance]);

        var services = Substitute.For<IContainerInstance>();
        services.Resolve<IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand>>()
            .Returns(Substitute.For<IEventSourceFunctionStateRepository<TradeSelectionFunctionState, ExecuteTradeSelectionPipelineCommand>>());
        services.Resolve<IFunctionProjector<TradeSelectionFunctionCompletedEvent>>()
            .Returns(Substitute.For<IFunctionProjector<TradeSelectionFunctionCompletedEvent>>());
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.Container.Returns(services);
        container.RegisterInstance(supervisor);
        container.RegisterInstance<ILogger<TradeSelectionFunctionActor>>(NullLogger<TradeSelectionFunctionActor>.Instance);

        var registrations = container.GetCurrentRegistrations();
        registrations.Single(value => value.ServiceType == typeof(ITradeSelectionFunctionContext)).Registration
            .Should().BeSameAs(registrations.Single(value =>
                value.ServiceType == typeof(IFunctionActorContext<TradeSelectionFunctionActor>)).Registration);
        var typed = container.GetInstance<ITradeSelectionFunctionContext>();
        typed.CalculationModel.Should().BeOfType<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model.TradeSelectionEvaluator>();
        container.GetInstance<IFunctionActorContext<TradeSelectionFunctionActor>>().Should().BeSameAs(typed);
        container.GetInstance<IActor<TradeSelectionFunctionActor>>().Should().BeOfType<TradeSelectionFunctionActor>();
    }
}
