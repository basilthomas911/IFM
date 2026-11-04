using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.TestSupport;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorForwardPriceCreationCheckpointTests
{
    [Fact]
    public void Creation_and_checkpoint_restore_complete_result_and_exact_duplicate_matching()
    {
        var request = IronCondorForwardPriceTestScenario.Request();
        var first = new IronCondorForwardPriceCalculatedEvent { CommandId = request.CommandId, EntityId = request.EntityId,
            RequestFingerprint = request.Fingerprint(), IronCondorForwardPrice = new() { ForwardPrice = 1, PositionSequence = 1 } };
        var middle = first with { IronCondorForwardPrice = first.IronCondorForwardPrice with { ForwardPrice = 2, PositionSequence = 2 } };
        var last = first with { IronCondorForwardPrice = first.IronCondorForwardPrice with { ForwardPrice = 3, PositionSequence = 3 } };
        var full = new IronCondorForwardPriceFunctionState();
        var optimized = new IronCondorForwardPriceFunctionState();
        full.ReplayEvents([Row(first, 1), Row(middle, 2), Row(last, 3)]);
        optimized.ReplayEvents([Row(first, 1), Row(last, 3)]);
        full.Prepare(request); optimized.Prepare(request);
        Assert.Equivalent(full.CompletedEvent, optimized.CompletedEvent);
        Assert.True(optimized.Matches(request));
        Assert.Equal(3, optimized.CommittedStreamVersion);
        Assert.Empty(optimized.Events);
        optimized.Prepare(request with { CommandId = Guid.NewGuid() });
        Assert.False(optimized.IsCompleted);
    }

    static EventStreamReadModel Row(IronCondorForwardPriceCalculatedEvent fact, long version) => new()
    {
        EventVersion = version, StreamVersion = version, EventTypeName = typeof(IronCondorForwardPriceCalculatedEvent).AssemblyQualifiedName!,
        EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(fact)
    };
}
