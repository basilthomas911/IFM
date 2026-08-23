using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Extensions;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Extensions;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Actor;
using TomasAI.IFM.Domain.Fund.Transaction.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

/// <summary>
/// Verifies the typed runtime contexts used by the Fund transaction actors.
/// </summary>
public sealed class FundTransactionContextTests
{
    [Fact]
    public void Command_context_exposes_dependencies_and_resolves_container_services_once()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var logger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var dbEventSource = Substitute.For<IEventSourceActorDbContext>();
        var durableReplayQueue = Substitute.For<IDurableReplayQueue>();
        var stateFactory = Substitute.For<IEventSourceActorStateFactory>();
        var actorService = Substitute.For<IActorService>();
        var eventProjector = Substitute.For<IEventProjector<FundTransactionCommandActor>>();
        supervisor.Container.Returns(container);
        container.Resolve<IEventSourceActorDbContext>().Returns(dbEventSource);
        container.Resolve<IDurableReplayQueue>().Returns(durableReplayQueue);
        container.Resolve<IEventSourceActorStateFactory>().Returns(stateFactory);
        container.Resolve<IActorService>().Returns(actorService);
        container.Resolve<IEventProjector<FundTransactionCommandActor>>().Returns(eventProjector);

        var context = new FundTransactionCommandContext(supervisor, dbFactory, blackboard, logger);
        ICommandActorContext<FundTransactionCommandActor> typedContext = context;

        context.Should().BeAssignableTo<CommandActorContext>();
        context.Should().BeAssignableTo<IFundTransactionCommandContext>();
        context.ActorId.Should().Be(
            new ActorMailboxId(ActorType.Command, FundTransactionCommandActor.ActorName));
        typedContext.DbFactory.Should().BeSameAs(dbFactory);
        typedContext.BlackboardService.Should().BeSameAs(blackboard);
        typedContext.Logger.Should().BeSameAs(logger);
        typedContext.DbEventSource.Should().BeSameAs(dbEventSource);
        typedContext.DurableReplayQueue.Should().BeSameAs(durableReplayQueue);
        typedContext.StateFactory.Should().BeSameAs(stateFactory);
        typedContext.ActorService.Should().BeSameAs(actorService);
        typedContext.EventProjector.Should().BeSameAs(eventProjector);

        _ = typedContext.DbEventSource;
        _ = typedContext.DurableReplayQueue;
        _ = typedContext.StateFactory;
        _ = typedContext.ActorService;
        _ = typedContext.EventProjector;
        container.Received(1).Resolve<IEventSourceActorDbContext>();
        container.Received(1).Resolve<IDurableReplayQueue>();
        container.Received(1).Resolve<IEventSourceActorStateFactory>();
        container.Received(1).Resolve<IActorService>();
        container.Received(1).Resolve<IEventProjector<FundTransactionCommandActor>>();
    }

    [Fact]
    public void Event_context_exposes_supervisor_and_logger()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var logger = Substitute.For<ILogger<FundTransactionEventActor>>();
        supervisor.Container.Returns(container);

        var context = new FundTransactionEventContext(supervisor, logger);
        IEventActorContext<FundTransactionEventActor> typedContext = context;

        context.Should().BeAssignableTo<EventActorContext>();
        context.Should().BeAssignableTo<IFundTransactionEventContext>();
        context.ActorId.Should().Be(
            new ActorMailboxId(ActorType.Event, FundTransactionEventActor.Actor));
        typedContext.Supervisor.Should().BeSameAs(supervisor);
        typedContext.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Query_context_exposes_database_factory_and_logger()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FundTransactionQueryActor>>();
        supervisor.Container.Returns(container);

        var context = new FundTransactionQueryContext(supervisor, dbFactory, logger);
        IQueryActorContext<FundTransactionQueryActor> typedContext = context;

        context.Should().BeAssignableTo<QueryActorContext>();
        context.Should().BeAssignableTo<IFundTransactionQueryContext>();
        context.ActorId.Should().Be(
            new ActorMailboxId(ActorType.Query, FundTransactionQueryActor.ActorName));
        typedContext.DbFactory.Should().BeSameAs(dbFactory);
        typedContext.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Context_constructors_reject_null_required_dependencies()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        var commandLogger = Substitute.For<ILogger<FundTransactionCommandActor>>();
        var eventLogger = Substitute.For<ILogger<FundTransactionEventActor>>();
        var queryLogger = Substitute.For<ILogger<FundTransactionQueryActor>>();

        var nullCommandSupervisor = () =>
            new FundTransactionCommandContext(null!, dbFactory, blackboard, commandLogger);
        var nullCommandFactory = () =>
            new FundTransactionCommandContext(supervisor, null!, blackboard, commandLogger);
        var nullCommandBlackboard = () =>
            new FundTransactionCommandContext(supervisor, dbFactory, null!, commandLogger);
        var nullCommandLogger = () =>
            new FundTransactionCommandContext(supervisor, dbFactory, blackboard, null!);
        var nullEventSupervisor = () => new FundTransactionEventContext(null!, eventLogger);
        var nullEventLogger = () => new FundTransactionEventContext(supervisor, null!);
        var nullQuerySupervisor = () => new FundTransactionQueryContext(null!, dbFactory, queryLogger);
        var nullQueryFactory = () => new FundTransactionQueryContext(supervisor, null!, queryLogger);
        var nullQueryLogger = () => new FundTransactionQueryContext(supervisor, dbFactory, null!);

        nullCommandSupervisor.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
        nullCommandFactory.Should().Throw<ArgumentNullException>().WithParameterName("dbFactory");
        nullCommandBlackboard.Should().Throw<ArgumentNullException>().WithParameterName("blackboardService");
        nullCommandLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        nullEventSupervisor.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
        nullEventLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        nullQuerySupervisor.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
        nullQueryFactory.Should().Throw<ArgumentNullException>().WithParameterName("dbFactory");
        nullQueryLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
