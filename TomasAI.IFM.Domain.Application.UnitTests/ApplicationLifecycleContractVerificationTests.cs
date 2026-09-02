using MessagePack;
using TomasAI.IFM.Domain.Application.Shared;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationLifecycleContractVerificationTests
{
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
        Assert.Equal(7, copy.Activities.Length);
        Assert.Equal(status.Activities.Select(value => value.Activity), copy.Activities.Select(value => value.Activity));
    }
}
