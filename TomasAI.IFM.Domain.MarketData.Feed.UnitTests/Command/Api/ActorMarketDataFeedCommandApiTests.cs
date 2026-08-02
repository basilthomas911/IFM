using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Api;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
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
}
