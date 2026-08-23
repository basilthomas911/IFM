using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Event.Actor;
using TomasAI.IFM.Domain.Fund.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

/// <summary>
/// Verifies the construction and Fund-specific services exposed by <see cref="FundEventContext"/>.
/// </summary>
public sealed class FundEventContextTests
{
    [Fact]
    public void Constructor_exposes_runtime_and_fund_services()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var logger = Substitute.For<ILogger<FundEventActor>>();
        supervisor.Container.Returns(container);

        var context = new FundEventContext(supervisor, logger);

        context.Should().BeAssignableTo<EventActorContext>();
        context.Should().BeAssignableTo<IEventActorContext<FundEventActor>>();
        context.Should().BeAssignableTo<IFundEventContext>();
        context.ActorId.Should().Be(new ActorMailboxId(ActorType.Event, FundEventActor.Actor));
        context.Container.Should().BeSameAs(container);
        context.Supervisor.Should().BeSameAs(supervisor);
        context.Logger.Should().BeSameAs(logger);

        IEventActorContext<FundEventActor> genericContext = context;
        genericContext.Supervisor.Should().BeSameAs(supervisor);
        genericContext.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Constructor_rejects_null_supervisor()
    {
        var logger = Substitute.For<ILogger<FundEventActor>>();

        var action = () => new FundEventContext(null!, logger);

        action.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
    }

    [Fact]
    public void Constructor_rejects_null_logger()
    {
        var supervisor = Substitute.For<IActorSupervisor>();

        var nullLogger = () => new FundEventContext(supervisor, null!);

        nullLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
