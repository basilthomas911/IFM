using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.TickAggregation;

public sealed class TickAggregationCommandAuditTests
{
    [Fact]
    public async Task New_command_is_logged_and_not_reported_as_completed_retry()
    {
        var command = Command();
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandLogAsync(command, Arg.Any<DateTime>(), Arg.Any<string>()).Returns(true);
        var audit = new TickAggregationCommandAuditTracker(db);

        audit.Start(command);
        var completedRetry = await audit.CompleteAsync(command);

        Assert.False(completedRetry);
        await db.Received(1).TryInsertCommandLogAsync(command, Arg.Any<DateTime>(), Arg.Any<string>());
        await db.DidNotReceive().GetCommandLogAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Exact_existing_command_with_event_is_a_successful_completed_retry()
    {
        var command = Command();
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandLogAsync(command, Arg.Any<DateTime>(), Arg.Any<string>()).Returns(false);
        db.GetCommandLogAsync(command.CommandId).Returns(new CommandLogReadModel(
            command.CommandId,
            command.StreamId,
            command.RouteTo,
            command.CommandName,
            DateTime.UtcNow,
            TickAggregationCommandAuditTracker.CreateFingerprint(command)));
        db.HasEventForCommandAsync(command.CommandId).Returns(true);
        var audit = new TickAggregationCommandAuditTracker(db);

        audit.Start(command);

        Assert.True(await audit.CompleteAsync(command));
        await db.DidNotReceive().InsertCommandLogAsync(
            Arg.Any<TomasAI.IFM.Shared.EventSourcing.ICommand>(),
            Arg.Any<DateTime>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Reused_command_id_with_different_content_fails()
    {
        var command = Command();
        var db = Substitute.For<IEventSourceActorDbContext>();
        db.TryInsertCommandLogAsync(command, Arg.Any<DateTime>(), Arg.Any<string>()).Returns(false);
        db.GetCommandLogAsync(command.CommandId).Returns(new CommandLogReadModel(
            command.CommandId,
            command.StreamId,
            command.RouteTo,
            command.CommandName,
            DateTime.UtcNow,
            "different"));
        var audit = new TickAggregationCommandAuditTracker(db);

        audit.Start(command);
        await Assert.ThrowsAsync<InvalidOperationException>(() => audit.CompleteAsync(command).AsTask());
    }

    private static InsertFuturesTickTradeDataCommand Command()
    {
        var entity = new TickDataEntityId("ESU6", new DateOnly(2026, 8, 7), AssetTypeId.Futures);
        return new InsertFuturesTickTradeDataCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, InsertFuturesTickTradeDataCommand.Actor,
                InsertFuturesTickTradeDataCommand.Verb, entity.Format()),
            EntityId = entity,
            TickDataId = new TickDataId(entity.ContractId, entity.ValueDate, 1,
                new DateTime(2026, 8, 7, 20, 0, 0, DateTimeKind.Utc)),
            AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3",
            DefinitionDate = entity.ValueDate,
            PublisherId = 1,
            InstrumentId = 42,
            TradeData = new FuturesTickTradeData(1, 2, 3, 0, 5_000_000_000, 5m, 1, 0, 0, 0)
        };
    }
}
