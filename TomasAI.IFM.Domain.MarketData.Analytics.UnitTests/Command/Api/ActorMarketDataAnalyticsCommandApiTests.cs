using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Command.Api;

public class ActorMarketDataAnalyticsCommandApiTests
{
    [Fact]
    public async Task GenerateFuturesRsiSignalUsesTheBoundEventContextAndReturnsItsResult()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var signalId = new FuturesRsiSignalId(
            "ESZ6", new DateOnly(2026, 8, 2), TimeFrameType.Daily, 14, new TimeOnly(16, 0));
        context.RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Any<GenerateFuturesRsiSignalCommand>())
            .Returns(expected);
        var api = context;

        var result = await api.GenerateFuturesRsiSignalAsync(signalId, 6425.25m);

        result.Should().BeSameAs(expected);
        await context.Received(1)
            .RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Is<GenerateFuturesRsiSignalCommand>(command =>
                    command.FuturesRsiSignalId == signalId &&
                    command.FuturesPrice == 6425.25m &&
                    command.CommandId != Guid.Empty &&
                    command.ErrorCode == GenerateFuturesRsiSignalCommand.ErrorId &&
                    command.Subject.Is(
                        ActorType.Command,
                        GenerateFuturesRsiSignalCommand.Actor,
                        GenerateFuturesRsiSignalCommand.Verb)));
    }

    [Fact]
    public async Task PeriodSignalGenerationAssignsUniqueNonEmptyCommandIds()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var commands = new List<ICommand>();
        context.RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Do<GenerateFuturesRsiSignalCommand>(commands.Add))
            .Returns(expected);
        context.RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(
                Arg.Do<GenerateFuturesAtrSignalCommand>(commands.Add))
            .Returns(expected);
        context.RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(
                Arg.Do<GenerateFuturesAdxSignalCommand>(commands.Add))
            .Returns(expected);
        context.RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(
                Arg.Do<GenerateFuturesMacdSignalCommand>(commands.Add))
            .Returns(expected);
        var api = context;
        var valueDate = new DateOnly(2026, 8, 17);
        var timestamp = new TimeOnly(12, 30);

        await api.GenerateFuturesRsiSignalAsync(
            new FuturesRsiSignalId("ESU6", valueDate, TimeFrameType.FifteenSeconds, 13, timestamp),
            6425.25m);
        await api.GenerateFuturesRsiSignalAsync(
            new FuturesRsiSignalId("ESU6", valueDate, TimeFrameType.OneMinute, 13, timestamp),
            6425.25m);
        await api.GenerateFuturesAtrSignalAsync(
            new FuturesAtrSignalId("ESU6", valueDate, TimeFrameType.FifteenSeconds, 14, timestamp),
            6425.25m);
        await api.GenerateFuturesAdxSignalAsync(
            new FuturesAdxSignalId("ESU6", valueDate, TimeFrameType.FifteenSeconds, 14, timestamp),
            6425.25m);
        await api.GenerateFuturesMacdSignalAsync(
            new FuturesMacdSignalId("ESU6", valueDate, TimeFrameType.FifteenSeconds, 9, 12, 26, timestamp),
            6425.25m);

        commands.Should().HaveCount(5);
        commands.Select(command => command.CommandId)
            .Should().NotContain(Guid.Empty)
            .And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task FailedCommandResultIsRaisedToTheCallingEventHandler()
    {
        var context = Substitute.For<IEventActorContext>();
        var signalId = new FuturesRsiSignalId(
            "ESZ6", new DateOnly(2026, 8, 2), TimeFrameType.Daily, 14, new TimeOnly(16, 0));
        context.RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Any<GenerateFuturesRsiSignalCommand>())
            .Returns(new ServiceFailed<GuidResult>(GenerateFuturesRsiSignalCommand.ErrorId, "generation failed"));
        var api = context;

        Func<Task> act = async () => await api.GenerateFuturesRsiSignalAsync(signalId, 6425.25m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("generation failed");
    }
}
