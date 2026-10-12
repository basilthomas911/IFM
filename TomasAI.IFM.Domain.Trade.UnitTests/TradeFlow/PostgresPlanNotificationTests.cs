using TomasAI.IFM.Shared.EventProjector;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Position.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class PostgresPlanNotificationTests
{
    [Fact]
    public async Task Futures_notification_does_not_write_again_or_request_durable_replay()
    {
        var (factory, plans) = Storage();
        var context = Substitute.For<IFuturesPositionCommandContext>();
        context.DbFactory.Returns(factory);
        var projector = new FuturesPositionEventProjector(context);
        try { await AssertNotificationAsync(projector.ProjectionDescriptors, new FuturesTradePlanUpdatedEvent(), plans); }
        finally { await projector.StopAsync(); }
    }

    [Fact]
    public async Task Vertical_spread_notification_does_not_write_again_or_request_durable_replay()
    {
        var (factory, plans) = Storage();
        var context = Substitute.For<IVerticalSpreadPositionCommandContext>();
        context.DbFactory.Returns(factory);
        var projector = new VerticalSpreadPositionEventProjector(context);
        try { await AssertNotificationAsync(projector.ProjectionDescriptors, new VerticalSpreadTradePlanUpdatedEvent(), plans); }
        finally { await projector.StopAsync(); }
    }

    [Fact]
    public async Task Iron_condor_notification_does_not_write_again_or_request_durable_replay()
    {
        var (factory, plans) = Storage();
        var context = Substitute.For<IIronCondorPositionCommandContext>();
        context.DbFactory.Returns(factory);
        var projector = new IronCondorPositionEventProjector(context);
        try { await AssertNotificationAsync(projector.ProjectionDescriptors, new IronCondorTradePlanUpdatedEvent(), plans); }
        finally { await projector.StopAsync(); }
    }

    static (IDbContextFactory Factory, ITradePlanDbContext Plans) Storage()
    {
        var plans = Substitute.For<ITradePlanDbContext>();
        plans.ProviderName.Returns("System.Data.Postgres");
        plans.DbWriter.Returns(plans);
        plans.ProjectMaterialAsync(Arg.Any<StrategyTradePlanSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("A committed snapshot must not be written again.")));
        var factory = Substitute.For<IDbContextFactory>();
        factory.TradePlanDb.Returns(plans);
        return (factory, plans);
    }

    static async Task AssertNotificationAsync(IReadOnlyCollection<EventProjectionDescriptor> descriptors,
        IEvent source, ITradePlanDbContext plans)
    {
        var descriptor = descriptors.Single(x => x.SourceEventType == source.GetType());
        descriptor.UseDurableReplay.Should().BeFalse();
        descriptor.PublishTerminalEvent.Should().BeFalse();
        descriptor.ApplySnapshotAsync.Should().NotBeNull();
        ((TomasAI.IFM.Shared.EventSourcing.IRequireDurableProjection)source).RequiresDurableProjection.Should().BeFalse();
        var result = await descriptor.ApplySnapshotAsync!(source, CancellationToken.None);
        result.Outcome.Should().Be(EventProjectionApplyOutcome.Applied);
        await plans.DidNotReceiveWithAnyArgs().ProjectMaterialAsync(default!, default);
    }
}
