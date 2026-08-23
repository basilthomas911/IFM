using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Command.Actor;
using TomasAI.IFM.Domain.Fund.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

/// <summary>
/// Verifies the construction and Fund-specific services exposed by <see cref="FundCommandContext"/>.
/// </summary>
public sealed class FundCommandContextTests
{
    [Fact]
    public void Constructor_exposes_runtime_and_fund_services()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        var actorService = Substitute.For<IActorService>();
        var eventProjector = Substitute.For<IEventProjector<FundCommandActor>>();
        supervisor.Container.Returns(container);
        container.Resolve<IEventSourceActorDbContext>().Returns(dbEventSource);
        container.Resolve<IDurableReplayQueue>().Returns(durableReplayQueue);
        container.Resolve<IEventSourceActorStateFactory>().Returns(stateFactory);
        container.Resolve<IActorService>().Returns(actorService);
        container.Resolve<IEventProjector<FundCommandActor>>().Returns(eventProjector);

        var context = new FundCommandContext(supervisor, dbFactory, blackboard, logger);

        context.Should().BeAssignableTo<CommandActorContext>();
        context.Should().BeAssignableTo<ICommandActorContext<FundCommandActor>>();
        context.Should().BeAssignableTo<IFundCommandContext>();
        context.ActorId.Should().Be(new ActorMailboxId(ActorType.Command, FundCommandActor.Actor));
        context.Container.Should().BeSameAs(container);
        context.DbFactory.Should().BeSameAs(dbFactory);
        context.BlackboardService.Should().BeSameAs(blackboard);
        context.Logger.Should().BeSameAs(logger);
        ICommandActorContext<FundCommandActor> genericContext = context;
        genericContext.DbFactory.Should().BeSameAs(dbFactory);
        genericContext.BlackboardService.Should().BeSameAs(blackboard);
        genericContext.Logger.Should().BeSameAs(logger);
        genericContext.DbEventSource.Should().BeSameAs(dbEventSource);
        genericContext.DurableReplayQueue.Should().BeSameAs(durableReplayQueue);
        genericContext.StateFactory.Should().BeSameAs(stateFactory);
        genericContext.ActorService.Should().BeSameAs(actorService);
        genericContext.EventProjector.Should().BeSameAs(eventProjector);

        genericContext.DbEventSource.Should().BeSameAs(dbEventSource);
        genericContext.DurableReplayQueue.Should().BeSameAs(durableReplayQueue);
        genericContext.StateFactory.Should().BeSameAs(stateFactory);
        genericContext.ActorService.Should().BeSameAs(actorService);
        genericContext.EventProjector.Should().BeSameAs(eventProjector);
        container.Received(1).Resolve<IEventSourceActorDbContext>();
        container.Received(1).Resolve<IDurableReplayQueue>();
        container.Received(1).Resolve<IEventSourceActorStateFactory>();
        container.Received(1).Resolve<IActorService>();
        container.Received(1).Resolve<IEventProjector<FundCommandActor>>();
    }

    [Fact]
    public void Constructor_rejects_null_supervisor()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();

        var action = () => new FundCommandContext(null!, dbFactory, blackboard, logger);

        action.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
    }

    [Fact]
    public void Constructor_rejects_null_fund_dependencies()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var logger = Substitute.For<ILogger<FundCommandActor>>();

        var nullFactory = () => new FundCommandContext(supervisor, null!, blackboard, logger);
        var nullBlackboard = () => new FundCommandContext(supervisor, dbFactory, null!, logger);
        var nullLogger = () => new FundCommandContext(supervisor, dbFactory, blackboard, null!);

        nullFactory.Should().Throw<ArgumentNullException>().WithParameterName("dbFactory");
        nullBlackboard.Should().Throw<ArgumentNullException>().WithParameterName("blackboardService");
        nullLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
