using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterEventProjectorDescriptorTests
{
    [Fact]
    public void Source_only_parameter_projections_do_not_request_terminal_events()
    {
        var set = Substitute.For<IParameterSetCommandContext>();
        set.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        set.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        set.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        set.Logger.Returns(Substitute.For<ILogger<ParameterSetCommandActor>>());
        var assignment = Substitute.For<IParameterAssignmentCommandContext>();
        assignment.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        assignment.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        assignment.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        assignment.Logger.Returns(Substitute.For<ILogger<ParameterAssignmentCommandActor>>());
        var startup = Substitute.For<IParameterStartupCommandContext>();
        startup.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        startup.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        startup.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        startup.Logger.Returns(Substitute.For<ILogger<ParameterStartupCommandActor>>());

        new ParameterSetEventProjector(set).ProjectionDescriptors
            .Should().OnlyContain(descriptor => !descriptor.PublishTerminalEvent);
        new ParameterAssignmentEventProjector(assignment).ProjectionDescriptors
            .Should().OnlyContain(descriptor => !descriptor.PublishTerminalEvent);
        new ParameterStartupEventProjector(startup).ProjectionDescriptors
            .Should().OnlyContain(descriptor => !descriptor.PublishTerminalEvent);
    }

}
