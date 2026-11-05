using NSubstitute;
using TomasAI.IFM.Application.Identity;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ProducerOperations;
using TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function;
using TomasAI.IFM.Domain.MarketData.Feed.StreamingRequestIdAllocation.Function.State;

namespace TomasAI.IFM.Domain.MarketData.Feed.BDDTests.MarketDataFeed;

public sealed class StreamingRequestAllocationTests
{
    [Fact]
    public async Task Given_AnAllocatedId_When_TheSameOperationIsRetried_Then_TheCompletionMatches()
    {
        var sequences = Substitute.For<ISequenceIdApi>();
        sequences.GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId, CancellationToken.None)
            .Returns(701L);
        var request = AllocateStreamingRequestIdCommand.ForOperation(Guid.NewGuid());

        var completed = await request.ExecuteAsync(sequences, CancellationToken.None);
        var state = new StreamingRequestIdAllocationFunctionState();
        Assert.True(state.TryComplete(completed, request));
        Assert.True(state.Matches(request));
        Assert.Equal(701, state.CompletedEvent!.Value);
        Assert.False(state.Matches(AllocateStreamingRequestIdCommand.ForOperation(Guid.NewGuid())));
        await sequences.Received(1).GetSequenceIdAsync(SequenceName.StreamingRequest_RequestId, CancellationToken.None);
    }
}
