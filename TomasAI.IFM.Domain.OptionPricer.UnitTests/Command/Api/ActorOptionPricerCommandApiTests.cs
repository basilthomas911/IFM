using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Command.Api;

public class ActorOptionPricerCommandApiTests
{
    [Fact]
    public async Task CompleteJobUsesTheBoundEventContextAndReturnsItsResult()
    {
        var context = Substitute.For<IEventActorContext>();
        var expected = new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
        var entityId = new SpreadDistributionJobEntityId(101, 7, new DateOnly(2026, 8, 2));
        var completed = new DateTime(2026, 8, 2, 15, 30, 0, DateTimeKind.Utc);
        context.RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(
                Arg.Any<CompleteSpreadDistributionJobCommand>())
            .Returns(expected);
        var api = context;

        var result = await api.CompleteSpreadDistributionJobAsync(
            entityId, completed, SpreadDistributionJobStatus.Completed);

        result.Should().BeSameAs(expected);
        await context.Received(1)
            .RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(
                Arg.Is<CompleteSpreadDistributionJobCommand>(command =>
                    command.EntityId == entityId &&
                    command.JobCompleted == completed &&
                    command.JobStatus == SpreadDistributionJobStatus.Completed &&
                    command.CommandId != Guid.Empty &&
                    command.ErrorCode == CompleteSpreadDistributionJobCommand.ErrorId &&
                    command.Subject.Is(
                        ActorType.Command,
                        CompleteSpreadDistributionJobCommand.Actor,
                        CompleteSpreadDistributionJobCommand.Verb)));
    }

    [Fact]
    public async Task FailedCommandResultIsRaisedToTheCallingEventHandler()
    {
        var context = Substitute.For<IEventActorContext>();
        var entityId = new SpreadDistributionJobEntityId(101, 7, new DateOnly(2026, 8, 2));
        context.RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(
                Arg.Any<CompleteSpreadDistributionJobCommand>())
            .Returns(new ServiceFailed<GuidResult>(CompleteSpreadDistributionJobCommand.ErrorId, "job failed"));
        var api = context;

        Func<Task> act = async () => await api.CompleteSpreadDistributionJobAsync(
            entityId, DateTime.UtcNow, SpreadDistributionJobStatus.Completed);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("job failed");
    }
}
