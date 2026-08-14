using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Option.Command.EventProjector;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Option;

public sealed class OptionTradeEventProjectorTests
{
    [Fact]
    public void Every_current_option_trade_event_has_a_unique_durable_descriptor()
    {
        var projector = new OptionTradeEventProjector(
            Substitute.For<IDbContextFactory>(),
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<OptionTradeEventProjector>>());

        projector.ProjectionDescriptors.Should().HaveCount(16);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems();
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
        projector.ProjectionDescriptors.Single(descriptor => descriptor.SourceEventType.Name == "TradePositionAddedEvent")
            .PublishProcessingEvent.Should().BeFalse("the legacy event has no typed actor-delivery contract");
    }

    [Fact]
    public async Task Option_trade_spread_replay_uses_the_persisted_event_id_as_its_stable_sequence_key()
    {
        const long persistedEventId = 918275;
        var tradeDb = Substitute.For<ITradeDbContext>();
        var dbFactory = Substitute.For<IDbContextFactory>();
        dbFactory.TradeDb.Returns(tradeDb);
        var projector = new OptionTradeEventProjector(
            dbFactory,
            Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(),
            Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<OptionTradeEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(OptionTradeSpreadDataInsertedEvent));
        var source = new OptionTradeSpreadDataInsertedEvent
        {
            OptionTradeSpreadData = new OptionTradeSpreadsDataModel
            {
                OrderId = 10,
                TradeId = 20,
                ValueDate = new DateOnly(2026, 8, 13),
                SequenceId = 0
            }
        };
        var context = new ProjectionExecutionContext(
            projector.ProjectorName,
            persistedEventId,
            eventStreamId: 42,
            new EventProjectorEffectIdentity(
                projector.ProjectorName,
                persistedEventId,
                EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            CancellationToken.None,
            streamVersion: 7);

        await descriptor.ApplyAsync(source, context);
        await descriptor.ApplyAsync(source, context);

        await tradeDb.Received(2).InsertOptionTradeSpreadDataAsync(
            Arg.Is<OptionTradeSpreadsDataModel>(row => row.SequenceId == persistedEventId));
    }
}
