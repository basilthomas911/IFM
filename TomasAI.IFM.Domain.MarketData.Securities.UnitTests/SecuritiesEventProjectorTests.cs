using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Securities.UnitTests;

public sealed class SecuritiesEventProjectorTests
{
    [Fact]
    public void Security_mutations_are_unique_and_durable()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var queue = Substitute.For<IDurableReplayQueue>();
        var eventSource = Substitute.For<IEventSourceActorDbContext>();
        var blackboard = Substitute.For<IBlackboardService>();
        IEventProjector[] projectors =
        [
            new FuturesContractEventProjector(dbFactory, queue, eventSource, blackboard, Substitute.For<ILogger<FuturesContractEventProjector>>()),
            new FuturesOptionContractEventProjector(dbFactory, Substitute.For<IActorService>(), queue, eventSource, blackboard, Substitute.For<ILogger<FuturesOptionContractEventProjector>>())
        ];

        projectors.SelectMany(projector => projector.ProjectionDescriptors).Should().HaveCount(7);
        projectors.SelectMany(projector => projector.ProjectionDescriptors)
            .Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
        foreach (var projector in projectors)
            projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType).Should().OnlyHaveUniqueItems();
    }
}
