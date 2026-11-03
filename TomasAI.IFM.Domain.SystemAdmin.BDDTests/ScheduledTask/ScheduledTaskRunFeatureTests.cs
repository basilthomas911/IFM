using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

public sealed class ScheduledTaskRunFeatureTests
{
    [Fact]
    [Trait("Command", nameof(RequestScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_new_scheduled_occurrence_when_requested_then_its_identity_and_target_are_recorded()
    {
        var given = new ScheduledTaskScenario();
        given.ThenRunAccepted<ScheduledTaskRunRequestedEvent>(given.RequestRun(), actual =>
        {
            Assert.Equal(given.Id, actual.ScheduleId); Assert.Equal(given.RunId, actual.Id); Assert.Equal(given.Fire, actual.IntendedFireTimeUtc); Assert.Equal(ScheduledTaskRunStatus.Requested, actual.Status); Assert.Null(actual.ProcessId);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunAdmissionCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_requested_run_when_admission_is_confirmed_then_it_is_admitted_without_claiming_process_start()
    {
        var given = new ScheduledTaskScenario().GivenRequestedRun();
        given.ThenRunAccepted<ScheduledTaskRunAdmissionRecordedEvent>(given.RunAdmission(), actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Admitted, actual.Status); Assert.Null(actual.ProcessId); Assert.Null(actual.FinishedAtUtc);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunStartedCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_admitted_run_when_the_host_confirms_process_start_then_running_process_identity_is_recorded()
    {
        var given = new ScheduledTaskScenario().GivenAdmittedRun();
        given.ThenRunAccepted<ScheduledTaskRunStartedEvent>(given.Start(), actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Running, actual.Status); Assert.Equal(1234, actual.ProcessId); Assert.Equal(given.Fire.AddSeconds(1), actual.StartedAtUtc);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunStageCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_running_task_when_a_business_stage_is_confirmed_then_its_value_date_and_detail_are_recorded()
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        given.ThenRunAccepted<ScheduledTaskRunStageRecordedEvent>(given.Stage(), actual =>
        {
            Assert.Equal("FeedsStopped", actual.Stage); Assert.Equal(ScheduledTaskScenario.ValueDate, actual.ValueDate); Assert.Equal(ScheduledTaskRunStatus.Running, actual.Status); Assert.Null(actual.CompletedEndOfDayValueDate);
        });
    }

    [Fact]
    [Trait("Command", nameof(CompleteScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_running_task_when_successful_business_completion_is_confirmed_then_the_run_succeeds()
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        given.ThenRunAccepted<ScheduledTaskRunCompletedEvent>(given.Complete(), actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Succeeded, actual.Status); Assert.Equal(0, actual.ExitCode); Assert.Equal(ScheduledTaskScenario.Now, actual.FinishedAtUtc); Assert.Equal("BusinessCompleted", actual.Stage);
        });
    }

    [Fact]
    [Trait("Command", nameof(FailScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_running_task_when_the_host_reports_failure_then_failure_and_diagnostics_are_recorded()
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        given.ThenRunAccepted<ScheduledTaskRunFailedEvent>(given.Fail(), actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Failed, actual.Status); Assert.Equal(1, actual.ExitCode); Assert.Equal("task reported failure", actual.Detail); Assert.Equal(ScheduledTaskScenario.Now, actual.FinishedAtUtc);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunUncertainCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_running_task_when_its_outcome_cannot_be_confirmed_then_uncertainty_is_recorded_without_claiming_success()
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        given.ThenRunAccepted<ScheduledTaskRunUncertainRecordedEvent>(given.Uncertain(), actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Uncertain, actual.Status); Assert.Null(actual.ExitCode); Assert.Equal("host outcome could not be confirmed", actual.Detail);
        });
    }

    [Theory]
    [Trait("Command", nameof(RequestScheduledTaskRunCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("already_requested", "ALREADY_REQUESTED")]
    [InlineData("future_occurrence", "REQUEST.INVALID")]
    [InlineData("wrong_occurrence_identity", "REQUEST.INVALID")]
    [InlineData("missing_host", "REQUEST.INVALID")]
    [InlineData("invalid_definition_revision", "REQUEST.INVALID")]
    public void Given_RequestScheduledTaskRun_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RequestScheduledTaskRunCommand command;
        switch (edge)
        {
            case "already_requested":
                given.GivenRequestedRun();
                command = given.RequestRun();
                break;
            case "future_occurrence":

                command = given.RequestRun() with { IntendedFireTimeUtc = ScheduledTaskScenario.Now.AddSeconds(1) };
                break;
            case "wrong_occurrence_identity":

                command = given.RequestRun() with { EntityId = new(Guid.NewGuid()) };
                break;
            case "missing_host":

                command = given.RequestRun() with { HostId = "" };
                break;
            case "invalid_definition_revision":

                command = given.RequestRun() with { DefinitionRevision = 0 };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskRunAdmissionCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("before_request", "NOT_FOUND")]
    [InlineData("already_admitted", "ADMISSION.OUT_OF_ORDER")]
    [InlineData("already_finished", "TERMINAL")]
    public void Given_RecordScheduledTaskRunAdmission_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskRunAdmissionCommand command;
        switch (edge)
        {
            case "before_request":

                command = given.RunAdmission();
                break;
            case "already_admitted":
                given.GivenAdmittedRun();
                command = given.RunAdmission();
                break;
            case "already_finished":
                given.GivenRunningRun(); Assert.True(given.Send(given.Complete()).Success);
                command = given.RunAdmission();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskRunStartedCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("not_admitted", "START.INVALID")]
    [InlineData("invalid_process", "START.INVALID")]
    [InlineData("before_intended_fire", "START.INVALID")]
    [InlineData("future_start", "START.INVALID")]
    public void Given_RecordScheduledTaskRunStarted_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskRunStartedCommand command;
        switch (edge)
        {
            case "not_admitted":
                given.GivenRequestedRun();
                command = given.Start();
                break;
            case "invalid_process":
                given.GivenAdmittedRun();
                command = given.Start() with { ProcessId = 0 };
                break;
            case "before_intended_fire":
                given.GivenAdmittedRun();
                command = given.Start() with { StartedAtUtc = given.Fire.AddTicks(-1) };
                break;
            case "future_start":
                given.GivenAdmittedRun();
                command = given.Start() with { StartedAtUtc = ScheduledTaskScenario.Now.AddTicks(1) };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskRunStageCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("not_running", "STAGE.INVALID")]
    [InlineData("missing_value_date", "STAGE.INVALID")]
    [InlineData("empty_business_stage", "STAGE.INVALID")]
    [InlineData("changed_value_date", "STAGE.INVALID")]
    [InlineData("ordinary_task_cannot_finalize_positions", "EOD.OUT_OF_ORDER")]
    public void Given_RecordScheduledTaskRunStage_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskRunStageCommand command;
        switch (edge)
        {
            case "not_running":
                given.GivenAdmittedRun();
                command = given.Stage();
                break;
            case "missing_value_date":
                given.GivenRunningRun();
                command = given.Stage() with { ValueDate = default };
                break;
            case "empty_business_stage":
                given.GivenRunningRun();
                command = given.Stage() with { Stage = "" };
                break;
            case "changed_value_date":
                given.GivenRunningRun(); Assert.True(given.Send(given.Stage()).Success);
                command = given.Stage() with { ValueDate = ScheduledTaskScenario.ValueDate.AddDays(1) };
                break;
            case "ordinary_task_cannot_finalize_positions":
                given.GivenRunningRun(); Assert.True(given.Send(given.Stage()).Success);
                command = given.Stage() with { Stage = "PositionsFinalized" };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(CompleteScheduledTaskRunCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("not_running", "COMPLETION.INVALID")]
    [InlineData("failed_exit", "COMPLETION.INVALID")]
    [InlineData("finish_before_process_start", "COMPLETION.INVALID")]
    [InlineData("future_finish", "COMPLETION.INVALID")]
    [InlineData("missing_business_completion_stage", "COMPLETION.INVALID")]
    [InlineData("uncertain_is_not_success", "TERMINAL")]
    [InlineData("duplicate_completion", "TERMINAL")]
    public void Given_CompleteScheduledTaskRun_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        CompleteScheduledTaskRunCommand command;
        switch (edge)
        {
            case "not_running":
                given.GivenAdmittedRun();
                command = given.Complete();
                break;
            case "failed_exit":
                given.GivenRunningRun();
                command = given.Complete() with { ExitCode = 1 };
                break;
            case "finish_before_process_start":
                given.GivenRunningRun();
                command = given.Complete() with { FinishedAtUtc = given.Fire };
                break;
            case "future_finish":
                given.GivenRunningRun();
                command = given.Complete() with { FinishedAtUtc = ScheduledTaskScenario.Now.AddTicks(1) };
                break;
            case "missing_business_completion_stage":
                given.GivenRunningRun();
                command = given.Complete() with { Stage = "" };
                break;
            case "uncertain_is_not_success":
                given.GivenRunningRun(); Assert.True(given.Send(given.Uncertain()).Success);
                command = given.Complete();
                break;
            case "duplicate_completion":
                given.GivenRunningRun(); Assert.True(given.Send(given.Complete()).Success);
                command = given.Complete();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(FailScheduledTaskRunCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("finish_before_process_start", "FAILURE.INVALID")]
    [InlineData("future_finish", "FAILURE.INVALID")]
    [InlineData("before_request", "NOT_FOUND")]
    [InlineData("uncertain_requires_explicit_resolution", "RESOLUTION.INVALID")]
    [InlineData("uncertain_requires_current_revision", "RESOLUTION.INVALID")]
    [InlineData("uncertain_requires_named_operator", "RESOLUTION.INVALID")]
    [InlineData("uncertain_requires_review_reason", "RESOLUTION.INVALID")]
    public void Given_FailScheduledTaskRun_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        FailScheduledTaskRunCommand command;
        switch (edge)
        {
            case "finish_before_process_start":
                given.GivenRunningRun();
                command = given.Fail() with { FinishedAtUtc = given.Fire };
                break;
            case "future_finish":
                given.GivenRunningRun();
                command = given.Fail() with { FinishedAtUtc = ScheduledTaskScenario.Now.AddTicks(1) };
                break;
            case "before_request":

                command = given.Fail();
                break;
            case "uncertain_requires_explicit_resolution":
                given.GivenRunningRun(); Assert.True(given.Send(given.Uncertain()).Success);
                command = given.Fail();
                break;
            case "uncertain_requires_current_revision":
                given.GivenRunningRun(); Assert.True(given.Send(given.Uncertain()).Success);
                command = given.Fail() with { Stage = "OperatorResolved", ExpectedRevision = 1 };
                break;
            case "uncertain_requires_named_operator":
                given.GivenRunningRun(); Assert.True(given.Send(given.Uncertain()).Success);
                command = given.Fail() with { Stage = "OperatorResolved", ExpectedRevision = given.Run.Revision, Operator = "" };
                break;
            case "uncertain_requires_review_reason":
                given.GivenRunningRun(); Assert.True(given.Send(given.Uncertain()).Success);
                command = given.Fail() with { Stage = "OperatorResolved", ExpectedRevision = given.Run.Revision, Reason = "" };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskRunUncertainCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("finish_before_process_start", "UNCERTAIN.INVALID")]
    [InlineData("future_finish", "UNCERTAIN.INVALID")]
    [InlineData("already_succeeded", "TERMINAL")]
    public void Given_RecordScheduledTaskRunUncertain_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskRunUncertainCommand command;
        switch (edge)
        {
            case "finish_before_process_start":
                given.GivenRunningRun();
                command = given.Uncertain() with { FinishedAtUtc = given.Fire };
                break;
            case "future_finish":
                given.GivenRunningRun();
                command = given.Uncertain() with { FinishedAtUtc = ScheduledTaskScenario.Now.AddTicks(1) };
                break;
            case "already_succeeded":
                given.GivenRunningRun(); Assert.True(given.Send(given.Complete()).Success);
                command = given.Uncertain();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenRunRejected(command, rejection);
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunAdmissionCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_requested_run_when_admission_is_denied_then_the_rejected_outcome_is_recorded_successfully()
    {
        var given = new ScheduledTaskScenario().GivenRequestedRun();
        given.ThenRunAccepted<ScheduledTaskRunAdmissionRecordedEvent>(given.RunAdmission() with { Accepted = false }, actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Rejected, actual.Status);
            Assert.Equal(ScheduledTaskScenario.Now, actual.FinishedAtUtc);
            Assert.Null(actual.ProcessId);
        });
    }

    [Fact]
    [Trait("Command", nameof(RequestScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_manual_request_when_its_identity_is_not_a_cron_occurrence_then_the_explicit_request_is_retained()
    {
        var given = new ScheduledTaskScenario();
        var manualId = new ScheduledTaskId(Guid.NewGuid());
        given.ThenRunAccepted<ScheduledTaskRunRequestedEvent>(given.RequestRun() with { EntityId = manualId, Manual = true,
            Subject = new(ActorType.Command, RequestScheduledTaskRunCommand.Actor, RequestScheduledTaskRunCommand.Verb, manualId.Format()) },
            actual => { Assert.Equal(manualId, actual.Id); Assert.True(actual.Manual); });
    }

    [Fact]
    [Trait("Command", nameof(FailScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_uncertain_run_when_an_operator_resolves_its_current_revision_then_reviewed_failure_releases_uncertainty()
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        Assert.True(given.Send(given.Uncertain()).Success);
        given.ThenRunAccepted<ScheduledTaskRunFailedEvent>(given.Fail() with
            { Stage = "OperatorResolved", ExpectedRevision = given.Run.Revision, Reason = "confirmed process terminated" }, actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Failed, actual.Status);
            Assert.Equal("OperatorResolved", actual.Stage);
            Assert.Equal("confirmed process terminated", actual.Reason);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunStageCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_market_close_with_confirmed_feed_stop_when_positions_finalize_then_the_completed_EOD_date_is_recorded()
    {
        var given = new ScheduledTaskScenario(ScheduledTaskKeys.FuturesMarketClose).GivenRunningRun();
        Assert.True(given.Send(given.Stage()).Success);
        given.ThenRunAccepted<ScheduledTaskRunStageRecordedEvent>(given.Stage() with { Stage = "PositionsFinalized" }, actual =>
        {
            Assert.Equal(ScheduledTaskScenario.ValueDate, actual.CompletedEndOfDayValueDate);
            Assert.Equal(ScheduledTaskRunStatus.Running, actual.Status);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunStageCommand))]
    [Trait("Scenario", "EdgeCase")]
    public void Given_market_close_without_feed_stop_when_positions_finalize_then_finalization_is_rejected()
    {
        var given = new ScheduledTaskScenario(ScheduledTaskKeys.FuturesMarketClose).GivenRunningRun();
        given.ThenRunRejected(given.Stage() with { Stage = "PositionsFinalized" }, "EOD.OUT_OF_ORDER");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Command", nameof(CompleteScheduledTaskRunCommand))]
    [Trait("Command", nameof(FailScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_large_diagnostic_output_when_a_run_finishes_then_only_bounded_output_tails_are_persisted(bool failed)
    {
        var given = new ScheduledTaskScenario().GivenRunningRun();
        var output = new string('x', 17000) + "last output";
        var error = new string('y', 17000) + "last error";
        void Then(ScheduledTaskRun actual)
        {
            Assert.Equal(output[^16384..], actual.StandardOutputTail);
            Assert.Equal(error[^16384..], actual.StandardErrorTail);
            Assert.Equal("isolated/run-artifacts", actual.OutputDirectory);
        }
        if (failed)
            given.ThenRunAccepted<ScheduledTaskRunFailedEvent>(given.Fail() with
                { StandardOutputTail = output, StandardErrorTail = error, OutputDirectory = "isolated/run-artifacts" }, Then);
        else
            given.ThenRunAccepted<ScheduledTaskRunCompletedEvent>(given.Complete() with
                { StandardOutputTail = output, StandardErrorTail = error, OutputDirectory = "isolated/run-artifacts" }, Then);
    }
    [Fact]
    [Trait("Command", nameof(FailScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_confirmed_EOD_finalization_when_later_maintenance_fails_then_the_completed_business_date_is_preserved()
    {
        var given = new ScheduledTaskScenario(ScheduledTaskKeys.FuturesMarketClose).GivenRunningRun();
        Assert.True(given.Send(given.Stage()).Success);
        Assert.True(given.Send(given.Stage() with { Stage = "PositionsFinalized" }).Success);
        given.ThenRunAccepted<ScheduledTaskRunFailedEvent>(given.Fail() with { Stage = "MaintenanceFailed" }, actual =>
        {
            Assert.Equal(ScheduledTaskRunStatus.Failed, actual.Status);
            Assert.Equal(ScheduledTaskScenario.ValueDate, actual.CompletedEndOfDayValueDate);
        });
    }
}
