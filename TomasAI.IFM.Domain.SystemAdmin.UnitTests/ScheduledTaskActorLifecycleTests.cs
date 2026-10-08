using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;
public sealed class ScheduledTaskActorLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 21, 1, 0, TimeSpan.Zero);
    private static readonly ScheduledTaskId Id = new(Guid.NewGuid());
    private static readonly ScheduledTaskSchedule Schedule = new() { TaskKey = ScheduledTaskKeys.FuturesMarketClose, Name = "Market close", Expression = "0 1 17 ? * MON-FRI", MaximumRuntimeSeconds = 120 };
    private static ScheduledTaskCatalog Catalog => new() { Id = new(Guid.NewGuid()), HostId = "development", Environment = "Development", Projects = [new() { TaskKey = Schedule.TaskKey, ProjectName = "FuturesMarketClose", ManifestVersion = "1", ArtifactDigest = "sha256:abc", Platform = "Windows", Available = true, MaximumRuntimeSeconds = 300 }], HostCapability = new() { HostId = "development", Environment = "Development", Platform = "Windows", Ready = true, ObservedAtUtc = Now, Generation = 1 } };
    private static ScheduledTaskDefinition Installed => new() { Id = Id, Revision = 3, DesiredRevision = 2, AppliedRevision = 2, Schedule = Schedule, Enabled = true, AppliedEnabled = true, InstallationStatus = ScheduledTaskInstallationStatus.Applied, ManifestVersion = "1", ReviewedArtifactDigest = "sha256:abc" };
    [Fact]
    public void Same_manifest_with_changed_code_digest_requires_review_before_admission()
    {
        var changedCatalog = Catalog with { Projects = [Catalog.Projects[0] with { ArtifactDigest = "sha256:changed" }] };
        var model = ScheduledTaskComputation.Compute(new AdmitScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = Id,
            RunId = Guid.NewGuid(), DefinitionRevision = Installed.DesiredRevision, IntendedFireTimeUtc = Now, HostId = "development", Environment = "Development" }, Installed, Now, changedCatalog);
        Assert.False(model.Accepted); Assert.Contains("ARTIFACT_CHANGED", model.RejectionReason);
    }
    [Theory]
    [InlineData("America/New_York", 2026, 10, 7, 21)]
    [InlineData("Eastern Standard Time", 2026, 12, 7, 22)]
    public void CloseTimingUsesEasternDst(string zone, int year, int month, int day, int hour)
    {
        var date = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        var preview = ScheduledTaskTimingRules.Preview(Schedule with { TimeZoneId = zone }, date, 1);
        Assert.True(preview.Valid); Assert.Equal(date.AddHours(hour).AddMinutes(1), Assert.Single(preview.NextFireTimesUtc));
    }
    [Fact] public void PosixCronIsRejected() => Assert.False(ScheduledTaskTimingRules.Preview(Schedule with { Expression = "1 17 * * 1-5" }, Now).Valid);
    [Fact] public void EventCarriesOriginatingCommandAndComputedPayload()
    {
        var command = new CreateScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id, Schedule = Schedule, Subject = new(ActorType.Command, CreateScheduledTaskCommand.Actor, CreateScheduledTaskCommand.Verb, Id.Format()) };
        var state = new ScheduledTaskCommandState();
        Assert.IsType<ServiceOk<GuidResult>>(command.Execute(state, Now.AddMinutes(-1), Catalog));
        Assert.False(state.ScheduledTaskDefinition!.Enabled);
        var model = ScheduledTaskComputation.Compute(command, null, Now.AddMinutes(-1), Catalog);
        var source = new ScheduledTaskCreatedEvent { CommandId = command.CommandId, EntityId = Id, ScheduledTaskDefinition = model.ScheduledTaskDefinition, Subject = new(ActorType.Event, ScheduledTaskCreatedEvent.Actor, ScheduledTaskCreatedEvent.Verb, Id.Format()) };
        var copy = MessagePackSerializer.Deserialize<ScheduledTaskCreatedEvent>(MessagePackSerializer.Serialize(source));
        Assert.Equal(command.CommandId, copy.CommandId); Assert.Equal(Schedule.Expression, copy.ScheduledTaskDefinition!.Schedule.Expression);
    }
    [Fact] public void StaleOperatorEditDoesNotProduceModel()
    {
        var change = ScheduledTaskComputation.Compute(new DisableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id, ExpectedRevision = 1 }, Installed, Now, Catalog);
        Assert.False(change.Accepted); Assert.Contains("REVISION.CONFLICT", change.RejectionReason);
    }
    [Fact] public void InstallationReceiptCannotConfirmAnotherRevision()
    {
        var change = ScheduledTaskComputation.Compute(new RecordScheduledTaskInstallationCommand { CommandId = Guid.NewGuid(), EntityId = Id, DesiredRevision = 1, AppliedEnabled = true, Fingerprint = "f" }, Installed, Now, Catalog);
        Assert.False(change.Accepted); Assert.Contains("INSTALLATION.STALE", change.RejectionReason);
    }
    [Fact] public void ReadyHostAndAppliedDefinitionAdmitExactlyOneOccurrence()
    {
        var command = Admission();
        var first = ScheduledTaskComputation.Compute(command, Installed, Now, Catalog);
        Assert.True(first.Accepted);
        var duplicate = ScheduledTaskComputation.Compute(command, first.ScheduledTaskDefinition, Now, Catalog);
        Assert.False(duplicate.Accepted); Assert.Contains("OVERLAP", duplicate.RejectionReason);
        var released = ScheduledTaskComputation.Compute(new RecordScheduledTaskRunCompletionCommand { CommandId = Guid.NewGuid(), EntityId = Id, RunId = command.RunId }, first.ScheduledTaskDefinition, Now, Catalog);
        Assert.True(released.Accepted); Assert.Null(released.ScheduledTaskDefinition!.ActiveRunId);
        var redelivery = ScheduledTaskComputation.Compute(command, released.ScheduledTaskDefinition, Now, Catalog);
        Assert.False(redelivery.Accepted); Assert.Contains("DUPLICATE", redelivery.RejectionReason);
    }
    [Fact] public void UninstalledRevisionCannotLaunch() => Assert.Contains("NOT_INSTALLED", ScheduledTaskComputation.Compute(Admission(), Installed with { AppliedRevision = 1 }, Now, Catalog).RejectionReason);
    [Fact] public void LateOccurrenceCannotLaunch() => Assert.Contains("LATE", ScheduledTaskComputation.Compute(Admission(), Installed, Now.AddMinutes(2), Catalog).RejectionReason);
    [Fact] public void StaleHostCannotLaunch() => Assert.Contains("HOST.UNAVAILABLE", ScheduledTaskComputation.Compute(Admission(), Installed, Now, Catalog with { HostCapability = Catalog.HostCapability! with { ObservedAtUtc = Now.AddMinutes(-3) } }).RejectionReason);
    [Fact] public void WrongCronInstantCannotLaunch() => Assert.Contains("NOT_SCHEDULED", ScheduledTaskComputation.Compute(Admission() with { IntendedFireTimeUtc = Now.AddSeconds(-1) }, Installed, Now, Catalog).RejectionReason);
    [Fact] public void OccurrenceIdentityIgnoresOffsetRepresentation() => Assert.Equal(ScheduledTaskRunComputation.OccurrenceId(Id, Now), ScheduledTaskRunComputation.OccurrenceId(Id, Now.ToOffset(TimeSpan.FromHours(-4))));
    [Fact] public void UncertainRunIsTerminalAndCannotRelaunch()
    {
        var id = ScheduledTaskRunComputation.OccurrenceId(Id, Now);
        var run = ScheduledTaskRunComputation.Compute(new RequestScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = id, ScheduleId = Id, DefinitionRevision = 2, TaskKey = Schedule.TaskKey, HostId = "development", Environment = "Development", IntendedFireTimeUtc = Now }, null, Now).ScheduledTaskRun!;
        var admitted = ScheduledTaskRunComputation.Compute(new RecordScheduledTaskRunAdmissionCommand { CommandId = Guid.NewGuid(), EntityId = id, Accepted = true }, run, Now).ScheduledTaskRun!;
        var running = ScheduledTaskRunComputation.Compute(new RecordScheduledTaskRunStartedCommand { CommandId = Guid.NewGuid(), EntityId = id, ProcessId = 1, StartedAtUtc = Now }, admitted, Now).ScheduledTaskRun!;
        var uncertain = ScheduledTaskRunComputation.Compute(new RecordScheduledTaskRunUncertainCommand { CommandId = Guid.NewGuid(), EntityId = id, FinishedAtUtc = Now.AddSeconds(1) }, running, Now.AddSeconds(1)).ScheduledTaskRun!;
        var completion = ScheduledTaskRunComputation.Compute(new CompleteScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = id, FinishedAtUtc = Now.AddSeconds(2), ExitCode = 0, Stage = "complete" }, uncertain, Now.AddSeconds(2));
        Assert.False(completion.Accepted); Assert.Contains("TERMINAL", completion.RejectionReason);
    }
    [Fact]
    public void EndOfDay_completion_requires_feed_stop_and_survives_later_maintenance_failure()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var running = new ScheduledTaskRun { Id = id, Revision = 3, TaskKey = ScheduledTaskKeys.FuturesMarketClose, Status = ScheduledTaskRunStatus.Running, StartedAtUtc = Now, IntendedFireTimeUtc = Now };
        var finalized = new RecordScheduledTaskRunStageCommand { CommandId = Guid.NewGuid(), EntityId = id, Stage = "PositionsFinalized", ValueDate = new DateOnly(2026,10,7) };
        Assert.False(ScheduledTaskRunComputation.Compute(finalized, running, Now).Accepted);
        Assert.False(ScheduledTaskRunComputation.Compute(finalized, running with { TaskKey = ScheduledTaskKeys.FuturesMarketOpen, Stage = "FeedsStopped" }, Now).Accepted);
        var stopped = ScheduledTaskRunComputation.Compute(finalized with { Stage = "FeedsStopped" }, running, Now).ScheduledTaskRun!;
        Assert.Null(stopped.CompletedEndOfDayValueDate);
        var completed = ScheduledTaskRunComputation.Compute(finalized, stopped, Now).ScheduledTaskRun!;
        Assert.Equal(finalized.ValueDate, completed.CompletedEndOfDayValueDate);
        var failed = ScheduledTaskRunComputation.Compute(new FailScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = id, Stage = "Maintenance", Detail = "Backup unavailable", FinishedAtUtc = Now }, completed, Now).ScheduledTaskRun!;
        Assert.Equal(finalized.ValueDate, failed.CompletedEndOfDayValueDate);
    }
    [Fact]
    public async Task Only_persisted_EOD_completion_notification_advances_operational_date()
    {
        var clock = NSubstitute.Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        var authority = new TomasAI.IFM.Domain.MarketData.Shared.FuturesValueDateProvider(clock);
        var context = new TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor.ScheduledTaskEventContext(
            NSubstitute.Substitute.For<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorSupervisor>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor.ScheduledTaskEventActor>.Instance,
            authority);
        var receipt = new TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.ScheduledTaskRunStageRecordedCompleteEvent
        {
            CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()),
            ScheduledTaskRun = new() { TaskKey = ScheduledTaskKeys.FuturesMarketClose, Stage = "FeedsStopped" }
        };
        await TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.ScheduledTaskRunStageRecordedComplete.ExecuteAsync(receipt, context);
        Assert.Equal(new DateOnly(2026,10,7), authority.ValueDate);
        var finalized = receipt with { ScheduledTaskRun = receipt.ScheduledTaskRun with { Stage = "PositionsFinalized", CompletedEndOfDayValueDate = new DateOnly(2026,10,7) } };
        await TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.ScheduledTaskRunStageRecordedComplete.ExecuteAsync(finalized, context);
        Assert.Equal(new DateOnly(2026,10,8), authority.ValueDate);
        var valueDate = authority.ValueDate;
        await TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.ScheduledTaskRunStageRecordedComplete.ExecuteAsync(finalized, context);
        Assert.Equal(valueDate, authority.ValueDate);
    }
    [Fact]
    public void Process_completion_preserves_the_finalized_business_value_date()
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var date = new DateOnly(2026,10,7);
        var run = new ScheduledTaskRun { Id = id, Revision = 5, TaskKey = ScheduledTaskKeys.FuturesMarketClose,
            Status = ScheduledTaskRunStatus.Running, Stage = "PositionsFinalized", ValueDate = date,
            CompletedEndOfDayValueDate = date, IntendedFireTimeUtc = Now, StartedAtUtc = Now };
        var result = ScheduledTaskRunComputation.Compute(new CompleteScheduledTaskRunCommand
        { CommandId = Guid.NewGuid(), EntityId = id, FinishedAtUtc = Now, ExitCode = 0, Stage = "BusinessCompleted" }, run, Now);
        Assert.True(result.Accepted);
        Assert.Equal(date, result.ScheduledTaskRun!.ValueDate);
        Assert.Equal(date, result.ScheduledTaskRun.CompletedEndOfDayValueDate);
    }
    [Fact]
    public void Uncertain_resolution_requires_current_revision_and_explicit_review()
    {
        var run = new ScheduledTaskRun { Id = Id, Revision = 6, Status = ScheduledTaskRunStatus.Uncertain, IntendedFireTimeUtc = Now };
        var command = new FailScheduledTaskRunCommand { EntityId = Id, CommandId = Guid.NewGuid(), FinishedAtUtc = Now, Stage = "OperatorResolved", ExpectedRevision = 6, Operator = "reviewer", Reason = "Verified no business work remains; retain failed outcome." };
        Assert.False(ScheduledTaskRunComputation.Compute(command with { Reason = "" }, run, Now).Accepted);
        Assert.False(ScheduledTaskRunComputation.Compute(command with { ExpectedRevision = 5 }, run, Now).Accepted);
        var resolved = ScheduledTaskRunComputation.Compute(command, run, Now);
        Assert.True(resolved.Accepted); Assert.Equal(ScheduledTaskRunStatus.Failed, resolved.ScheduledTaskRun!.Status);
        Assert.Null(resolved.ScheduledTaskRun.CompletedEndOfDayValueDate);
    }
    [Fact]
    public void Expired_one_time_definition_cannot_be_enabled()
    {
        var definition = Installed with { Schedule = Schedule with { Timing = ScheduledTaskTiming.OneTime, StartsAtUtc = Now.AddMinutes(-1) } };
        Assert.False(ScheduledTaskComputation.Compute(new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id, ExpectedRevision = definition.Revision }, definition, Now, Catalog).Accepted);
    }
    private static AdmitScheduledTaskRunCommand Admission() => new() { CommandId = Guid.NewGuid(), EntityId = Id, RunId = Guid.NewGuid(), DefinitionRevision = 2, HostId = "development", Environment = "Development", IntendedFireTimeUtc = Now };
}
