using MessagePack;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationLifecycleContractVerificationTests
{
    [Fact]
    [Trait("Category", "Verification")]
    public void Startup_command_retains_its_six_wire_fields()
    {
        var valueDate = new DateOnly(2026, 9, 24);
        var source = new StartApplicationCommand(valueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, StartApplicationCommand.Actor,
                StartApplicationCommand.Verb, new ApplicationEntityId(valueDate).Format()),
            PostEvents = true
        };

        var copy = MessagePackSerializer.Deserialize<StartApplicationCommand>(
            MessagePackSerializer.Serialize(source));

        Assert.Equal(source.CommandId, copy.CommandId);
        Assert.Equal(source.Subject, copy.Subject);
        Assert.Equal(source.PostEvents, copy.PostEvents);
        Assert.Equal(source.EntityId, copy.EntityId);
        Assert.Equal(source.ErrorCode, copy.ErrorCode);
        Assert.Equal(source.RouteTo, copy.RouteTo);
    }

    [Fact]
    [Trait("Category", "Verification")]
    public void Startup_status_round_trips_with_every_activity_result()
    {
        var status = new ApplicationStartupStatus
        {
            State = ApplicationLifecycleState.Degraded,
            ValueDate = new(2026, 9, 2),
            ProcessBootId = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            StartedAtUtc = DateTime.SpecifyKind(new(2026, 9, 2, 22, 0, 0), DateTimeKind.Utc),
            CompletedAtUtc = DateTime.SpecifyKind(new(2026, 9, 2, 22, 0, 5), DateTimeKind.Utc),
            Activities = ApplicationStartupPlan.Activities.Select(definition => new ApplicationStartupActivityResult
            {
                Activity = definition.Activity,
                Required = definition.Required,
                Outcome = definition.Required
                    ? ApplicationStartupActivityOutcome.AlreadySatisfied
                    : ApplicationStartupActivityOutcome.Degraded,
                StartedAtUtc = DateTime.UtcNow,
                CompletedAtUtc = DateTime.UtcNow,
                Reason = definition.Activity.ToString()
            }).ToArray(),
            Summary = "verification"
        };

        var copy = MessagePackSerializer.Deserialize<ApplicationStartupStatus>(
            MessagePackSerializer.Serialize(status));

        Assert.Equal(status.State, copy.State);
        Assert.Equal(status.ValueDate, copy.ValueDate);
        Assert.Equal(status.CommandId, copy.CommandId);
        Assert.Equal(status.CorrelationId, copy.CorrelationId);
        Assert.Equal(ApplicationStartupPlan.Activities.Count, copy.Activities.Length);
        Assert.Equal(status.Activities.Select(value => value.Activity), copy.Activities.Select(value => value.Activity));
    }
}
