using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests.ScheduledTask;

public sealed class ScheduledTaskCatalogFeatureTests
{
    [Fact]
    [Trait("Command", nameof(RegisterScheduledTaskProjectCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_deployed_project_when_registered_then_the_catalog_contains_its_reviewable_artifact()
    {
        var given = new ScheduledTaskScenario();
        given.ThenCatalogAccepted<ScheduledTaskCatalogProjectRegisteredEvent>(given.Register(), actual =>
        {
            Assert.Equal(given.CatalogId, actual.Id);
            Assert.Equal("bdd-host", actual.HostId);
            Assert.Equal("Production", actual.Environment);
            Assert.Equivalent(given.Project, Assert.Single(actual.Projects), strict: true);
        });
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskHostCapabilityCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_a_registered_project_when_fresh_host_capability_arrives_then_the_host_becomes_ready_without_losing_projects()
    {
        var given = new ScheduledTaskScenario().GivenCatalog();
        given.ThenCatalogAccepted<ScheduledTaskCatalogHostCapabilityRecordedEvent>(given.Observe(), actual =>
        {
            Assert.NotNull(actual.HostCapability);
            Assert.True(actual.HostCapability.Ready);
            Assert.Equal(2, actual.HostCapability.Generation);
            Assert.Equivalent(given.Project, Assert.Single(actual.Projects), strict: true);
        });
    }

    [Fact]
    [Trait("Command", nameof(RegisterScheduledTaskProjectCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_two_projects_and_host_capability_when_one_artifact_is_replaced_then_other_catalog_evidence_is_retained()
    {
        var given = new ScheduledTaskScenario().GivenReadyCatalog();
        Assert.True(given.Send(given.Register() with { Project = given.Project with { TaskKey = "another-task" } }).Success);
        var replacement = given.Project with { ManifestVersion = "v2", ArtifactDigest = "sha256:new" };
        given.ThenCatalogAccepted<ScheduledTaskCatalogProjectRegisteredEvent>(given.Register() with { Project = replacement }, actual =>
        {
            Assert.Equal(2, actual.Projects.Length);
            Assert.Equivalent(replacement, Assert.Single(actual.Projects, p => p.TaskKey == given.TaskKey), strict: true);
            Assert.Contains(actual.Projects, p => p.TaskKey == "another-task" && p.ArtifactDigest == "sha256:reviewed");
            Assert.Equivalent(given.Capability, actual.HostCapability, strict: true);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Command", nameof(RecordScheduledTaskHostCapabilityCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_existing_capability_when_a_new_generation_or_later_observation_arrives_then_current_readiness_is_recorded(bool newGeneration)
    {
        var given = new ScheduledTaskScenario().GivenReadyCatalog();
        var next = newGeneration
            ? given.Capability with { Generation = 3, Ready = false }
            : given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now, Ready = false };
        given.ThenCatalogAccepted<ScheduledTaskCatalogHostCapabilityRecordedEvent>(given.Observe() with { HostCapability = next },
            actual => { Assert.Equivalent(next, actual.HostCapability, strict: true); Assert.Single(actual.Projects); });
    }

    [Theory]
    [Trait("Command", nameof(RegisterScheduledTaskProjectCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("missing_digest", "PROJECT.INVALID")]
    [InlineData("missing_task", "PROJECT.INVALID")]
    [InlineData("missing_manifest", "PROJECT.INVALID")]
    [InlineData("zero_runtime", "PROJECT.INVALID")]
    [InlineData("unbounded_runtime", "PROJECT.INVALID")]
    [InlineData("different_host", "HOST.MISMATCH")]
    [InlineData("different_environment", "HOST.MISMATCH")]
    [InlineData("different_catalog_identity", "IDENTITY.MISMATCH")]
    public void Given_RegisterScheduledTaskProject_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RegisterScheduledTaskProjectCommand command;
        switch (edge)
        {
            case "missing_digest":

                command = given.Register() with { Project = given.Project with { ArtifactDigest = "" } };
                break;
            case "missing_task":

                command = given.Register() with { Project = given.Project with { TaskKey = "" } };
                break;
            case "missing_manifest":

                command = given.Register() with { Project = given.Project with { ManifestVersion = "" } };
                break;
            case "zero_runtime":

                command = given.Register() with { Project = given.Project with { MaximumRuntimeSeconds = 0 } };
                break;
            case "unbounded_runtime":

                command = given.Register() with { Project = given.Project with { MaximumRuntimeSeconds = 86401 } };
                break;
            case "different_host":
                given.GivenCatalog();
                command = given.Register() with { HostId = "another-host" };
                break;
            case "different_environment":
                given.GivenCatalog();
                command = given.Register() with { Environment = "Development" };
                break;
            case "different_catalog_identity":
                given.GivenCatalog();
                command = given.Register() with { EntityId = new(Guid.NewGuid()) };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenCatalogRejected(command, rejection);
    }

    [Theory]
    [Trait("Command", nameof(RecordScheduledTaskHostCapabilityCommand))]
    [Trait("Scenario", "EdgeCase")]
    [InlineData("invalid_generation", "CAPABILITY.INVALID")]
    [InlineData("stale_observation", "CAPABILITY.INVALID")]
    [InlineData("future_observation", "CAPABILITY.INVALID")]
    [InlineData("duplicate_observation", "CAPABILITY.STALE")]
    [InlineData("older_generation", "CAPABILITY.STALE")]
    [InlineData("older_time_in_same_generation", "CAPABILITY.STALE")]
    [InlineData("different_host", "HOST.MISMATCH")]
    [InlineData("different_environment", "HOST.MISMATCH")]
    [InlineData("missing_platform", "CAPABILITY.INVALID")]
    [InlineData("different_catalog_identity", "IDENTITY.MISMATCH")]
    public void Given_RecordScheduledTaskHostCapability_guard_failure_when_requested_then_no_state_or_event_changes(string edge, string rejection)
    {
        var given = new ScheduledTaskScenario();
        RecordScheduledTaskHostCapabilityCommand command;
        switch (edge)
        {
            case "invalid_generation":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { Generation = 0 } };
                break;
            case "stale_observation":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddMinutes(-2).AddTicks(-1) } };
                break;
            case "future_observation":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddTicks(1) } };
                break;
            case "duplicate_observation":
                given.GivenReadyCatalog();
                command = given.Observe();
                break;
            case "older_generation":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { Generation = 1, ObservedAtUtc = ScheduledTaskScenario.Now } };
                break;
            case "older_time_in_same_generation":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddSeconds(-2) } };
                break;
            case "different_host":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { HostId = "another-host" } };
                break;
            case "different_environment":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { Environment = "Development" } };
                break;
            case "missing_platform":
                given.GivenReadyCatalog();
                command = given.Observe() with { HostCapability = given.Capability with { Platform = "" } };
                break;
            case "different_catalog_identity":
                given.GivenReadyCatalog();
                command = given.Observe() with { EntityId = new(Guid.NewGuid()) };
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
        given.ThenCatalogRejected(command, rejection);
    }

    [Fact]
    [Trait("Command", nameof(RecordScheduledTaskHostCapabilityCommand))]
    [Trait("Scenario", "HappyPath")]
    public void Given_host_observation_at_the_freshness_boundary_when_recorded_then_it_is_still_current()
    {
        var given = new ScheduledTaskScenario().GivenCatalog();
        var observation = given.Capability with { ObservedAtUtc = ScheduledTaskScenario.Now.AddMinutes(-2) };
        given.ThenCatalogAccepted<ScheduledTaskCatalogHostCapabilityRecordedEvent>(given.Observe() with
            { HostCapability = observation }, actual => Assert.Equal(observation.ObservedAtUtc, actual.HostCapability!.ObservedAtUtc));
    }
}
