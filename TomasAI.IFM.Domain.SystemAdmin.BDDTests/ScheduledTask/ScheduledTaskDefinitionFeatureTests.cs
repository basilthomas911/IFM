using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

public sealed class ScheduledTaskDefinitionFeatureTests
{
    [Fact]
    [Trait("Command", nameof(CreateScheduledTaskCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_valid_timing_when_a_task_is_created_then_it_is_disabled_and_pending_installation()
    {
        var given = new ScheduledTaskScenario();
        given.ThenDefinitionAccepted<ScheduledTaskCreatedEvent>(given.Create(), actual =>
        {
            Assert.False(actual.Enabled); Assert.Equal(1, actual.DesiredRevision); Assert.Equal(0, actual.AppliedRevision); Assert.Equal(ScheduledTaskInstallationStatus.Pending, actual.InstallationStatus); Assert.Equal(ScheduledTaskScenario.Now, actual.UpdatedAtUtc);
        });
    }

    [Fact]
    [Trait("Command", nameof(ChangeScheduledTaskScheduleCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_existing_task_when_timing_changes_then_a_new_desired_revision_requires_installation()
    {
        var given = new ScheduledTaskScenario().GivenDefinition();
        given.ThenDefinitionAccepted<ScheduledTaskScheduleChangedEvent>(given.Change(), actual =>
        {
            Assert.Equal("0 5 * ? * *", actual.Schedule.Expression); Assert.Equal(2, actual.DesiredRevision); Assert.Equal(ScheduledTaskInstallationStatus.Pending, actual.InstallationStatus);
        });
    }

    [Fact]
    [Trait("Command", nameof(EnableScheduledTaskCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_ready_host_and_reviewed_artifact_when_enabled_then_the_review_is_recorded_without_claiming_installation()
    {
        var given = new ScheduledTaskScenario().GivenDefinition().GivenReadyCatalog();
        given.ThenDefinitionAccepted<ScheduledTaskEnabledEvent>(given.Enable(), actual =>
        {
            Assert.True(actual.Enabled); Assert.False(actual.AppliedEnabled); Assert.Equal("v1", actual.ManifestVersion); Assert.Equal("sha256:reviewed", actual.ReviewedArtifactDigest); Assert.Equal(ScheduledTaskInstallationStatus.Pending, actual.InstallationStatus);
        });
    }

    [Fact]
    [Trait("Command", nameof(DisableScheduledTaskCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_active_occurrence_when_disabled_then_future_execution_is_disabled_without_releasing_its_owner()
    {
        var given = new ScheduledTaskScenario().GivenAdmittedOccurrence();
        given.ThenDefinitionAccepted<ScheduledTaskDisabledEvent>(given.Disable(), actual =>
        {
            Assert.False(actual.Enabled); Assert.Equal(given.AdmissionId, actual.ActiveRunId); Assert.Equal(ScheduledTaskInstallationStatus.Pending, actual.InstallationStatus);
        });
    }

    [Fact]
    [Trait("Command", nameof(RemoveScheduledTaskCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_disabled_idle_task_when_removed_then_its_definition_is_tombstoned()
    {
        var given = new ScheduledTaskScenario().GivenDefinition();
        given.ThenDefinitionAccepted<ScheduledTaskRemovedEvent>(given.Remove(), actual =>
        {
            Assert.True(actual.Removed); Assert.False(actual.Enabled); Assert.Equal(2, actual.DesiredRevision);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskInstallationCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_enabled_desired_revision_when_installation_is_confirmed_then_applied_state_matches_desired_state()
    {
        var given = new ScheduledTaskScenario().GivenEnabled();
        given.ThenDefinitionAccepted<ScheduledTaskInstallationRecordedEvent>(given.Installation(), actual =>
        {
            Assert.Equal(actual.DesiredRevision, actual.AppliedRevision); Assert.True(actual.AppliedEnabled); Assert.Equal(ScheduledTaskInstallationStatus.Applied, actual.InstallationStatus); Assert.Equal("installed-sha256", actual.InstallationFingerprint);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskInstallationFailureCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_pending_installation_when_the_host_reports_failure_then_failure_is_visible_without_claiming_it_was_applied()
    {
        var given = new ScheduledTaskScenario().GivenEnabled();
        given.ThenDefinitionAccepted<ScheduledTaskInstallationFailureRecordedEvent>(given.InstallationFailure(), actual =>
        {
            Assert.Equal(ScheduledTaskInstallationStatus.Failed, actual.InstallationStatus); Assert.Equal("runtime installation failed", actual.InstallationDetail); Assert.Equal(0, actual.AppliedRevision); Assert.False(actual.AppliedEnabled);
        });
    }

    [Fact]
    [Trait("Command", nameof(AdmitScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_current_installed_definition_when_its_scheduled_occurrence_arrives_then_one_run_owns_the_task()
    {
        var given = new ScheduledTaskScenario().GivenInstalled();
        given.ThenDefinitionAccepted<ScheduledTaskRunAdmittedEvent>(given.Admit(), actual =>
        {
            Assert.Equal(given.AdmissionId, actual.ActiveRunId); Assert.Equal(ScheduledTaskScenario.Now, actual.LastAdmittedFireUtc); Assert.Equal(actual.DesiredRevision, actual.AppliedRevision);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskRunCompletionCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_admitted_occurrence_when_its_completion_is_recorded_then_ownership_is_released_and_fire_history_is_retained()
    {
        var given = new ScheduledTaskScenario().GivenAdmittedOccurrence();
        given.ThenDefinitionAccepted<ScheduledTaskRunReleasedEvent>(given.Release(), actual =>
        {
            Assert.Null(actual.ActiveRunId); Assert.Equal(ScheduledTaskScenario.Now, actual.LastAdmittedFireUtc);
        });
    }

    [Theory]
    [Trait("Command", nameof(CreateScheduledTaskCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("already_exists", "ALREADY_EXISTS")]
    [InlineData("nonzero_initial_revision", "REVISION.CONFLICT")]
    [InlineData("invalid_cron", "SCHEDULE.INVALID")]
    [InlineData("unavailable_time_zone", "SCHEDULE.INVALID")]
    [InlineData("unbounded_runtime", "SCHEDULE.INVALID")]
    [InlineData("past_one_time_execution", "SCHEDULE.INVALID")]
    public void Given_CreateScheduledTask_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        CreateScheduledTaskCommand command;
        switch (edge)
        {
            case "already_exists":
                given.GivenDefinition();
                command = given.Create();
                break;
            case "nonzero_initial_revision":

                command = given.Create() with { ExpectedRevision = 1 };
                break;
            case "invalid_cron":

                command = given.Create() with { Schedule = given.Schedule with { Expression = "invalid" } };
                break;
            case "unavailable_time_zone":

                command = given.Create() with { Schedule = given.Schedule with { TimeZoneId = "Unavailable/BddZone" } };
                break;
            case "unbounded_runtime":

                command = given.Create() with { Schedule = given.Schedule with { MaximumRuntimeSeconds = 86401 } };
                break;
            case "past_one_time_execution":

                command = given.Create() with { Schedule = given.Schedule with { Timing = ScheduledTaskTiming.OneTime, StartsAtUtc = ScheduledTaskScenario.Now.AddSeconds(-1) } };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(ChangeScheduledTaskScheduleCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("stale_revision", "REVISION.CONFLICT")]
    [InlineData("different_host", "TARGET.IMMUTABLE")]
    [InlineData("different_environment", "TARGET.IMMUTABLE")]
    [InlineData("different_task", "TARGET.IMMUTABLE")]
    [InlineData("invalid_timing", "SCHEDULE.INVALID")]
    public void Given_ChangeScheduledTaskSchedule_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        ChangeScheduledTaskScheduleCommand command;
        switch (edge)
        {
            case "stale_revision":
                given.GivenDefinition();
                command = given.Change() with { ExpectedRevision = 0 };
                break;
            case "different_host":
                given.GivenDefinition();
                command = given.Change() with { Schedule = given.Schedule with { HostId = "another-host" } };
                break;
            case "different_environment":
                given.GivenDefinition();
                command = given.Change() with { Schedule = given.Schedule with { Environment = "Development" } };
                break;
            case "different_task":
                given.GivenDefinition();
                command = given.Change() with { Schedule = given.Schedule with { TaskKey = "another-task" } };
                break;
            case "invalid_timing":
                given.GivenDefinition();
                command = given.Change() with { Schedule = given.Schedule with { Expression = "invalid" } };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(EnableScheduledTaskCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("stale_revision", "REVISION.CONFLICT")]
    [InlineData("missing_evidence", "HOST.UNAVAILABLE")]
    [InlineData("wrong_catalog_identity", "CATALOG.INVALID")]
    [InlineData("unready_host", "HOST.UNAVAILABLE")]
    [InlineData("stale_host", "HOST.UNAVAILABLE")]
    [InlineData("future_host", "HOST.UNAVAILABLE")]
    [InlineData("unavailable_artifact", "ARTIFACT.UNAVAILABLE")]
    [InlineData("duplicate_projects", "ARTIFACT.UNAVAILABLE")]
    [InlineData("incompatible_platform", "ARTIFACT.UNAVAILABLE")]
    [InlineData("insufficient_runtime_limit", "ARTIFACT.UNAVAILABLE")]
    public void Given_EnableScheduledTask_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        EnableScheduledTaskCommand command;
        switch (edge)
        {
            case "stale_revision":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { ExpectedRevision = 0 };
                break;
            case "missing_evidence":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = null };
                break;
            case "wrong_catalog_identity":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { Id = new(Guid.NewGuid()) } };
                break;
            case "unready_host":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { HostCapability = given.Capability with { Ready = false } } };
                break;
            case "stale_host":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddMinutes(-2).AddTicks(-1) } } };
                break;
            case "future_host":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddSeconds(1) } } };
                break;
            case "unavailable_artifact":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { Projects = [given.Project with { Available = false }] } };
                break;
            case "duplicate_projects":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { Projects = [given.Project, given.Project] } };
                break;
            case "incompatible_platform":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { Projects = [given.Project with { Platform = "Linux" }] } };
                break;
            case "insufficient_runtime_limit":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Enable() with { CatalogEvidence = given.Catalog with { Projects = [given.Project with { MaximumRuntimeSeconds = 60 }] } };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(DisableScheduledTaskCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("stale_revision", "REVISION.CONFLICT")]
    [InlineData("removed_definition", "REMOVED")]
    public void Given_DisableScheduledTask_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        DisableScheduledTaskCommand command;
        switch (edge)
        {
            case "stale_revision":
                given.GivenEnabled();
                command = given.Disable() with { ExpectedRevision = 1 };
                break;
            case "removed_definition":
                given.GivenDefinition(); Assert.True(given.Send(given.Remove()).Success);
                command = given.Disable();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RemoveScheduledTaskCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("enabled_definition", "REMOVE.ACTIVE")]
    [InlineData("disabled_but_still_owned", "REMOVE.ACTIVE")]
    [InlineData("stale_revision", "REVISION.CONFLICT")]
    [InlineData("already_removed", "REMOVED")]
    public void Given_RemoveScheduledTask_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RemoveScheduledTaskCommand command;
        switch (edge)
        {
            case "enabled_definition":
                given.GivenEnabled();
                command = given.Remove();
                break;
            case "disabled_but_still_owned":
                given.GivenAdmittedOccurrence(); Assert.True(given.Send(given.Disable()).Success);
                command = given.Remove();
                break;
            case "stale_revision":
                given.GivenDefinition();
                command = given.Remove() with { ExpectedRevision = 0 };
                break;
            case "already_removed":
                given.GivenDefinition(); Assert.True(given.Send(given.Remove()).Success);
                command = given.Remove();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskInstallationCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("older_desired_revision", "INSTALLATION.STALE")]
    [InlineData("wrong_enabled_state", "INSTALLATION.STALE")]
    [InlineData("missing_fingerprint", "INSTALLATION.STALE")]
    public void Given_RecordScheduledTaskInstallation_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskInstallationCommand command;
        switch (edge)
        {
            case "older_desired_revision":
                given.GivenEnabled();
                command = given.Installation() with { DesiredRevision = 1 };
                break;
            case "wrong_enabled_state":
                given.GivenEnabled();
                command = given.Installation() with { AppliedEnabled = false };
                break;
            case "missing_fingerprint":
                given.GivenEnabled();
                command = given.Installation() with { Fingerprint = "" };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskInstallationFailureCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("older_desired_revision", "INSTALLATION.STALE")]
    public void Given_RecordScheduledTaskInstallationFailure_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskInstallationFailureCommand command;
        switch (edge)
        {
            case "older_desired_revision":
                given.GivenEnabled();
                command = given.InstallationFailure() with { DesiredRevision = 1 };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(AdmitScheduledTaskRunCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("disabled", "RUN.NOT_INSTALLED")]
    [InlineData("not_installed", "RUN.NOT_INSTALLED")]
    [InlineData("changed_but_not_reinstalled", "RUN.NOT_INSTALLED")]
    [InlineData("wrong_definition_revision", "RUN.TARGET_MISMATCH")]
    [InlineData("wrong_host", "RUN.TARGET_MISMATCH")]
    [InlineData("wrong_environment", "RUN.TARGET_MISMATCH")]
    [InlineData("empty_run_identity", "RUN.TARGET_MISMATCH")]
    [InlineData("not_a_scheduled_occurrence", "RUN.NOT_SCHEDULED")]
    [InlineData("overlapping_run", "RUN.OVERLAP")]
    [InlineData("late_occurrence", "RUN.LATE")]
    [InlineData("future_occurrence", "RUN.LATE")]
    [InlineData("before_start_bound", "RUN.OUTSIDE_BOUNDS")]
    [InlineData("duplicate_completed_occurrence", "RUN.DUPLICATE")]
    [InlineData("missing_evidence", "HOST.UNAVAILABLE")]
    [InlineData("changed_reviewed_artifact", "RUN.ARTIFACT_CHANGED")]
    public void Given_AdmitScheduledTaskRun_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        AdmitScheduledTaskRunCommand command;
        switch (edge)
        {
            case "disabled":
                given.GivenDefinition().GivenReadyCatalog();
                command = given.Admit();
                break;
            case "not_installed":
                given.GivenEnabled();
                command = given.Admit();
                break;
            case "changed_but_not_reinstalled":
                given.GivenInstalled(); Assert.True(given.Send(given.Change()).Success);
                command = given.Admit();
                break;
            case "wrong_definition_revision":
                given.GivenInstalled();
                command = given.Admit() with { DefinitionRevision = 1 };
                break;
            case "wrong_host":
                given.GivenInstalled();
                command = given.Admit() with { HostId = "another-host" };
                break;
            case "wrong_environment":
                given.GivenInstalled();
                command = given.Admit() with { Environment = "Development" };
                break;
            case "empty_run_identity":
                given.GivenInstalled();
                command = given.Admit() with { RunId = Guid.Empty };
                break;
            case "not_a_scheduled_occurrence":
                given.GivenInstalled();
                command = given.Admit() with { IntendedFireTimeUtc = ScheduledTaskScenario.Now.AddSeconds(-1) };
                break;
            case "overlapping_run":
                given.GivenAdmittedOccurrence();
                command = given.Admit();
                break;
            case "late_occurrence":
                given.GivenInstalled();
                command = given.Admit() with { IntendedFireTimeUtc = ScheduledTaskScenario.Now.AddMinutes(-2) };
                break;
            case "future_occurrence":
                given.GivenInstalled();
                command = given.Admit() with { IntendedFireTimeUtc = ScheduledTaskScenario.Now.AddMinutes(1) };
                break;
            case "before_start_bound":
                given.GivenReadyCatalog();
                Assert.True(given.Send(given.Create() with { Schedule = given.Schedule with
                    { StartsAtUtc = ScheduledTaskScenario.Now.AddMinutes(1) } }).Success);
                Assert.True(given.Send(given.Enable()).Success);
                Assert.True(given.Send(given.Installation()).Success);
                command = given.Admit();
                break;
            case "duplicate_completed_occurrence":
                given.GivenAdmittedOccurrence(); Assert.True(given.Send(given.Release()).Success);
                command = given.Admit();
                break;
            case "missing_evidence":
                given.GivenInstalled();
                command = given.Admit() with { CatalogEvidence = null };
                break;
            case "changed_reviewed_artifact":
                given.GivenInstalled();
                command = given.Admit() with { CatalogEvidence = given.Catalog with { Projects = [given.Project with { ArtifactDigest = "sha256:unreviewed" }] } };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskRunCompletionCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("different_run_owner", "RUN.OWNER_MISMATCH")]
    [InlineData("no_admitted_owner", "RUN.OWNER_MISMATCH")]
    public void Given_RecordScheduledTaskRunCompletion_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskRunCompletionCommand command;
        switch (edge)
        {
            case "different_run_owner":
                given.GivenAdmittedOccurrence();
                command = given.Release() with { RunId = Guid.NewGuid() };
                break;
            case "no_admitted_owner":
                given.GivenDefinition();
                command = given.Release();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenDefinitionRejected(command, rejection);
    }

    [Fact]
    [Trait("Command", nameof(AdmitScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_an_occurrence_at_the_dispatch_tolerance_boundary_when_admitted_then_the_boundary_is_inclusive()
    {
        var given = new ScheduledTaskScenario().GivenInstalled();
        var fire = ScheduledTaskScenario.Now.AddSeconds(-60);
        given.ThenDefinitionAccepted<ScheduledTaskRunAdmittedEvent>(given.Admit() with { IntendedFireTimeUtc = fire },
            actual => Assert.Equal(fire, actual.LastAdmittedFireUtc));
    }

    [Fact]
    [Trait("Command", nameof(EnableScheduledTaskCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_host_evidence_exactly_two_minutes_old_when_enabled_then_the_freshness_boundary_is_inclusive()
    {
        var given = new ScheduledTaskScenario().GivenDefinition().GivenReadyCatalog();
        var command = given.Enable() with { CatalogEvidence = given.Catalog with
            { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddMinutes(-2) } } };
        given.ThenDefinitionAccepted<ScheduledTaskEnabledEvent>(command, actual => Assert.True(actual.Enabled));
    }
    [Fact]
    [Trait("Command", nameof(AdmitScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_manual_run_between_cron_occurrences_when_other_admission_checks_pass_then_it_can_be_admitted()
    {
        var given = new ScheduledTaskScenario().GivenInstalled();
        var requested = ScheduledTaskScenario.Now.AddSeconds(-1);
        given.ThenDefinitionAccepted<ScheduledTaskRunAdmittedEvent>(given.Admit() with
            { Manual = true, IntendedFireTimeUtc = requested }, actual => Assert.Equal(requested, actual.LastAdmittedFireUtc));
    }

    [Fact]
    [Trait("Command", nameof(AdmitScheduledTaskRunCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_the_Development_market_lifecycle_exception_when_admission_is_requested_then_catalog_availability_does_not_block_it()
    {
        var given = new ScheduledTaskScenario(ScheduledTaskKeys.FuturesMarketClose);
        Assert.True(given.Send(given.Create() with { Schedule = given.Schedule with { Environment = "Development" } }).Success);
        given.ThenDefinitionAccepted<ScheduledTaskRunAdmittedEvent>(given.Admit() with
            { Environment = "Development", CatalogEvidence = null }, actual =>
        {
            Assert.Equal(given.AdmissionId, actual.ActiveRunId);
            Assert.False(actual.Enabled);
        });
    }
}
