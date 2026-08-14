using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.UnitTests.LookupType;

public sealed class LookupTypeEventProjectorTests
{
    [Fact]
    public void Lookup_mutations_are_unique_and_durable()
    {
        var projector = new LookupTypeEventProjector(
            Substitute.For<IDbContextFactory>(), Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(), Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<LookupTypeEventProjector>>());

        projector.ProjectionDescriptors.Should().HaveCount(3);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems();
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
    }
}
