using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Query.Actor;
using TomasAI.IFM.Domain.Fund.Query.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.UnitTests;

/// <summary>
/// Verifies the construction and Fund-specific services exposed by <see cref="FundQueryContext"/>.
/// </summary>
public sealed class FundQueryContextTests
{
    [Fact]
    public void Constructor_exposes_runtime_and_fund_services()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var container = Substitute.For<IContainerInstance>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FundQueryActor>>();
        supervisor.Container.Returns(container);

        var context = new FundQueryContext(supervisor, dbFactory, logger);

        context.Should().BeAssignableTo<QueryActorContext>();
        context.Should().BeAssignableTo<IQueryActorContext<FundQueryActor>>();
        context.Should().BeAssignableTo<IFundQueryContext>();
        context.ActorId.Should().Be(new ActorMailboxId(ActorType.Query, FundQueryActor.ActorName));
        context.Container.Should().BeSameAs(container);
        context.DbFactory.Should().BeSameAs(dbFactory);
        context.Logger.Should().BeSameAs(logger);

        IQueryActorContext<FundQueryActor> genericContext = context;
        genericContext.DbFactory.Should().BeSameAs(dbFactory);
        genericContext.Logger.Should().BeSameAs(logger);
    }

    [Fact]
    public void Constructor_rejects_null_supervisor()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var logger = Substitute.For<ILogger<FundQueryActor>>();

        var action = () => new FundQueryContext(null!, dbFactory, logger);

        action.Should().Throw<ArgumentNullException>().WithParameterName("supervisor");
    }

    [Fact]
    public void Constructor_rejects_null_database_factory()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var logger = Substitute.For<ILogger<FundQueryActor>>();

        var action = () => new FundQueryContext(supervisor, null!, logger);

        action.Should().Throw<ArgumentNullException>().WithParameterName("dbFactory");
    }

    [Fact]
    public void Constructor_rejects_null_logger()
    {
        var supervisor = Substitute.For<IActorSupervisor>();
        var dbFactory = Substitute.For<IDbContextFactory>();

        var action = () => new FundQueryContext(supervisor, dbFactory, null!);

        action.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
