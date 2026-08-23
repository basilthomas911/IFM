using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.LookupType.Command.Actor;
using TomasAI.IFM.Domain.Reference.LookupType.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.UnitTests.LookupType;

public sealed class LookupTypeEventProjectorTests
{
    [Fact]
    public void Lookup_mutations_are_unique_and_durable()
    {
        var context = Substitute.For<ILookupTypeCommandContext>();
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        context.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        context.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        context.Logger.Returns(Substitute.For<ILogger<LookupTypeCommandActor>>());
        var projector = new LookupTypeEventProjector(context);

        projector.ProjectionDescriptors.Should().HaveCount(3);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems();
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
    }
}
