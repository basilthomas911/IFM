using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Api;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Command.Api;

public class ActorMarketDataFeedCommandApiTests
{
    [Fact]
    public async Task DeleteStreamingRequestUsesItsOwnRoutingMetadataAndReturnsTheContextResult()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var feedId = new FeedId(17);
        context.RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(
                Arg.Any<DeleteStreamingRequestIdCommand>())
            .Returns(expected);
        var api = new ActorMarketDataFeedCommandApiFactory().Create(context);

        var result = await api.DeleteStreamingRequestIdAsync(feedId);

        result.Should().BeSameAs(expected);
        await context.Received(1).RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(
            Arg.Is<DeleteStreamingRequestIdCommand>(command =>
                command.FeedId == feedId &&
                command.EntityId == feedId &&
                command.ErrorCode == DeleteStreamingRequestIdCommand.ErrorId &&
                command.Subject.Is(
                    ActorType.Command,
                    DeleteStreamingRequestIdCommand.Actor,
                    DeleteStreamingRequestIdCommand.Verb)));
    }

    [Fact]
    public async Task FailedCommandResultIsRaisedToTheCallingEventHandler()
    {
        var context = Substitute.For<IEventActorContext>();
        var feedId = new FeedId(17);
        context.RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(
                Arg.Any<DeleteStreamingRequestIdCommand>())
            .Returns(new ServiceFailed<GuidResult>(DeleteStreamingRequestIdCommand.ErrorId, "delete failed"));
        var api = new ActorMarketDataFeedCommandApi(context);

        Func<Task> act = async () => await api.DeleteStreamingRequestIdAsync(feedId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("delete failed");
    }

    [Fact]
    public async Task StartupStreamingCommandsReceiveDistinctNonEmptyCommandIds()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        StartFuturesTickDataStreamingCommand? tickCommand = null;
        StartFuturesBarDataStreamingCommand? barCommand = null;
        context.RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(
                Arg.Any<StartFuturesTickDataStreamingCommand>())
            .Returns(callInfo =>
            {
                tickCommand = callInfo.Arg<StartFuturesTickDataStreamingCommand>();
                return expected;
            });
        context.RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(
                Arg.Any<StartFuturesBarDataStreamingCommand>())
            .Returns(callInfo =>
            {
                barCommand = callInfo.Arg<StartFuturesBarDataStreamingCommand>();
                return expected;
            });
        var api = new ActorMarketDataFeedCommandApi(context);
        var contract = SampleData.EsContract;
        var valueDate = SampleData.ValueDate;

        await api.StartFuturesTickDataStreamingAsync(
            contract,
            valueDate,
            resetStream: false,
            new FuturesDataId(contract.ContractId, valueDate));
        await api.StartFuturesBarDataStreamingAsync(
            [contract],
            valueDate,
            new FuturesBarDataStreamingId(valueDate));

        tickCommand.Should().NotBeNull();
        barCommand.Should().NotBeNull();
        tickCommand!.CommandId.Should().NotBeEmpty();
        barCommand!.CommandId.Should().NotBeEmpty();
        barCommand.CommandId.Should().NotBe(tickCommand.CommandId);
    }
}
