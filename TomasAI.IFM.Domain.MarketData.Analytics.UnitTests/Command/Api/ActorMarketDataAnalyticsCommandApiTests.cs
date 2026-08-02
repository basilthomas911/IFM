using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Command.Api;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
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
        var api = new ActorMarketDataAnalyticsCommandApiFactory().Create(context);

        var result = await api.GenerateFuturesRsiSignalAsync(signalId, 6425.25m);

        result.Should().BeSameAs(expected);
        await context.Received(1)
            .RequestAsync<GenerateFuturesRsiSignalCommand, FuturesRsiSignalEntityId>(
                Arg.Is<GenerateFuturesRsiSignalCommand>(command =>
                    command.FuturesRsiSignalId == signalId &&
                    command.FuturesPrice == 6425.25m &&
                    command.ErrorCode == GenerateFuturesRsiSignalCommand.ErrorId &&
                    command.Subject.Is(
                        ActorType.Command,
                        GenerateFuturesRsiSignalCommand.Actor,
                        GenerateFuturesRsiSignalCommand.Verb)));
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
        var api = new ActorMarketDataAnalyticsCommandApi(context);

        Func<Task> act = async () => await api.GenerateFuturesRsiSignalAsync(signalId, 6425.25m);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("generation failed");
    }
}
