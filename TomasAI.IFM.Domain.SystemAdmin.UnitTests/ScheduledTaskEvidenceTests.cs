using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskEvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);
    private static readonly ScheduledTaskId Id = new(Guid.NewGuid());
    private static ScheduledTaskCatalog Catalog => new()
    {
        Id = ScheduledTaskIdentities.Catalog("Development", "development"), Revision = 7,
        Environment = "Development", HostId = "development",
        HostCapability = new() { Environment = "Development", HostId = "development", Platform = "Windows", Ready = true, Generation = 1, ObservedAtUtc = Now },
        Projects = [new() { TaskKey = "qualification", Available = true, ManifestVersion = "v2", ArtifactDigest = "sha256:abc", Platform = "Windows", MaximumRuntimeSeconds = 300 }]
    };

    private static ScheduledTaskCommandState State()
    {
        var state = new ScheduledTaskCommandState();
        Assert.True(state.Apply(new ScheduledTaskCreatedEvent { EntityId = Id, ScheduledTaskDefinition = new()
        {
            Id = Id, Revision = 1, DesiredRevision = 1, AppliedRevision = 1, Enabled = true, AppliedEnabled = true,
            InstallationStatus = ScheduledTaskInstallationStatus.Applied, ManifestVersion = "v2", ReviewedArtifactDigest = "sha256:abc",
            Schedule = new() { Name = "Qualification", TaskKey = "qualification", Expression = "0 0 17 ? * MON-FRI", MaximumRuntimeSeconds = 120 }
        } }));
        state.AcceptChanges();
        return state;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Accepted_decision_uses_serialized_evidence_and_replays_without_catalog(bool admit)
    {
        var state = State();
        if (admit)
        {
            var command = MessagePackSerializer.Deserialize<AdmitScheduledTaskRunCommand>(MessagePackSerializer.Serialize(new AdmitScheduledTaskRunCommand
            {
                CommandId = Guid.NewGuid(), EntityId = Id, RunId = Guid.NewGuid(), DefinitionRevision = 1, Manual = true,
                IntendedFireTimeUtc = Now, Environment = "Development", HostId = "development", CatalogEvidence = Catalog,
                Subject = new(ActorType.Command, AdmitScheduledTaskRunCommand.Actor, AdmitScheduledTaskRunCommand.Verb, Id.Format())
            }));
            Assert.Equal(7, command.CatalogEvidence!.Revision);
            Assert.IsType<ServiceOk<GuidResult>>(command.Execute(ScheduledTaskConventionTests.Context(Now), state));
            Assert.Equal(command.RunId, state.ScheduledTaskDefinition!.ActiveRunId);
        }
        else
        {
            var command = MessagePackSerializer.Deserialize<EnableScheduledTaskCommand>(MessagePackSerializer.Serialize(new EnableScheduledTaskCommand
            {
                CommandId = Guid.NewGuid(), EntityId = Id, ExpectedRevision = 1, CatalogEvidence = Catalog,
                Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, Id.Format())
            }));
            Assert.Equal(7, command.CatalogEvidence!.Revision);
            Assert.IsType<ServiceOk<GuidResult>>(command.Execute(ScheduledTaskConventionTests.Context(Now), state));
            Assert.Equal("sha256:abc", state.ScheduledTaskDefinition!.ReviewedArtifactDigest);
        }
        var replayed = State();
        replayed.ReplayEvents(new DomainEventCollection(state.Events));
        Assert.Equal(state.ScheduledTaskDefinition, replayed.ScheduledTaskDefinition);
        Assert.Empty(replayed.Events);
    }

    public static IEnumerable<object?[]> InvalidEvidence()
    {
        yield return [null];
        yield return [Catalog with { Id = new(Guid.NewGuid()) }];
        yield return [Catalog with { Revision = 0 }];
        yield return [Catalog with { HostId = "other" }];
        yield return [Catalog with { Environment = "Production" }];
        yield return [Catalog with { HostCapability = Catalog.HostCapability! with { ObservedAtUtc = Now.AddMinutes(-3) } }];
        yield return [Catalog with { HostCapability = Catalog.HostCapability! with { ObservedAtUtc = Now.AddSeconds(1) } }];
        yield return [Catalog with { Projects = [Catalog.Projects[0], Catalog.Projects[0]] }];
        yield return [Catalog with { Projects = null! }];
        yield return [Catalog with { HostCapability = Catalog.HostCapability! with { Generation = 0 } }];
        yield return [Catalog with { HostCapability = Catalog.HostCapability! with { Platform = "" } }];
    }

    [Theory]
    [MemberData(nameof(InvalidEvidence))]
    public void Unacceptable_evidence_rejects_both_commands_without_pending_events(ScheduledTaskCatalog? evidence)
    {
        var state = State(); var accepted = state.ScheduledTaskDefinition;
        var enable = new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = Id, ExpectedRevision = 1, CatalogEvidence = evidence };
        Assert.IsType<ServiceFailed<GuidResult>>(enable.Execute(ScheduledTaskConventionTests.Context(Now), state));
        var admit = new AdmitScheduledTaskRunCommand { CommandId = Guid.NewGuid(), EntityId = Id, RunId = Guid.NewGuid(), DefinitionRevision = 1,
            Manual = true, IntendedFireTimeUtc = Now, Environment = "Development", HostId = "development", CatalogEvidence = evidence };
        Assert.IsType<ServiceFailed<GuidResult>>(admit.Execute(ScheduledTaskConventionTests.Context(Now), state));
        Assert.Same(accepted, state.ScheduledTaskDefinition); Assert.Empty(state.Events); Assert.False(state.Updated);
    }

    [Fact]
    public void Legacy_command_arrays_keep_existing_keys_and_decode_missing_evidence_as_null()
    {
        var enable = Legacy(new EnableScheduledTaskCommand { ExpectedRevision = 12, Operator = "operator", CatalogEvidence = Catalog });
        Assert.Equal(12, enable.ExpectedRevision); Assert.Equal("operator", enable.Operator); Assert.Null(enable.CatalogEvidence);
        var admit = Legacy(new AdmitScheduledTaskRunCommand { DefinitionRevision = 42, Manual = true, HostId = "host", Environment = "env", CatalogEvidence = Catalog });
        Assert.Equal(42, admit.DefinitionRevision); Assert.True(admit.Manual); Assert.Equal("host", admit.HostId); Assert.Equal("env", admit.Environment); Assert.Null(admit.CatalogEvidence);
    }

    private static T Legacy<T>(T command)
    {
        var bytes = MessagePackSerializer.Serialize(command);
        var reader = new MessagePackReader(bytes);
        var count = reader.ReadArrayHeader();
        var start = (int)reader.Consumed;
        for (var i = 0; i < count - 1; i++) reader.Skip();
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(count - 1);
        writer.WriteRaw(bytes.AsSpan(start, (int)reader.Consumed - start));
        writer.Flush();
        return MessagePackSerializer.Deserialize<T>(buffer.WrittenMemory);
    }
}
