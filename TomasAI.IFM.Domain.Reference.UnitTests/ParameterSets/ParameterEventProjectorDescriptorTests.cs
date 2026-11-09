using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.EventProjector;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.EventProjector;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Actor;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterEventProjectorDescriptorTests
{
    [Fact]
    public void Parameter_projections_publish_durable_terminal_events()
    {
        var set = Substitute.For<IParameterSetCommandContext>();
        set.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        set.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());

        set.Logger.Returns(Substitute.For<ILogger<ParameterSetCommandActor>>());
        var assignment = Substitute.For<IParameterAssignmentCommandContext>();
        assignment.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        assignment.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());

        assignment.Logger.Returns(Substitute.For<ILogger<ParameterAssignmentCommandActor>>());
        var startup = Substitute.For<IParameterStartupCommandContext>();
        startup.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        startup.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());

        startup.Logger.Returns(Substitute.For<ILogger<ParameterStartupCommandActor>>());

        new ParameterSetEventProjector(set).ProjectionDescriptors
            .Should().OnlyContain(descriptor => descriptor.UseDurableReplay && descriptor.PublishTerminalEvent);
        new ParameterAssignmentEventProjector(assignment).ProjectionDescriptors
            .Should().OnlyContain(descriptor => descriptor.UseDurableReplay && descriptor.PublishTerminalEvent);
        new ParameterStartupEventProjector(startup).ProjectionDescriptors
            .Should().OnlyContain(descriptor => descriptor.UseDurableReplay && descriptor.PublishTerminalEvent);
    }

}
