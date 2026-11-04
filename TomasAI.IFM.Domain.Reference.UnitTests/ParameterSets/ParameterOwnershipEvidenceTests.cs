using System.Buffers;
using System.Security.Cryptography;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
using TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.State;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

/// <summary>Protects ownership, freshness fencing and historical duplicate compatibility.</summary>
public sealed class ParameterOwnershipEvidenceTests
{
    [Fact]
    public void Each_command_context_exposes_only_its_own_authoritative_state_repository()
    {
        AssertRepositories(typeof(IParameterSetCommandContext), typeof(ParameterSetCommandState));
        AssertRepositories(typeof(IParameterAssignmentCommandContext), typeof(ParameterAssignmentCommandState));
        AssertRepositories(typeof(IParameterStartupCommandContext), typeof(ParameterStartupCommandState));
    }

    [Fact]
    public async Task Missing_and_stale_evidence_fail_before_any_owner_event_is_created()
    {
        var database = Substitute.For<IConfigurationDbContext>();
        database.GetParameterRevisionFenceAsync(CancellationToken.None).Returns(8L);
        var command = new AssignParameterVersionCommand();
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => ParameterEvidenceFence.DemandAsync(database, command));
        Assert.Equal("PARAM.EVIDENCE_REQUIRED", missing.Message);
        await database.DidNotReceive().GetParameterRevisionFenceAsync(CancellationToken.None);
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => ParameterEvidenceFence.DemandAsync(database, command with { Evidence = new(7) }));
        Assert.Equal("PARAM.EVIDENCE_STALE", stale.Message);
        await ParameterEvidenceFence.DemandAsync(database, command with { Evidence = new(8) });
    }

    [Fact]
    public async Task Startup_requires_explicit_assignments_and_versions_instead_of_loading_foreign_state()
    {
        var id = Guid.NewGuid();
        var command = new ApplySignalStartupPlanCommand { CommandId = id, RunId = id, EntityId = new(id) };
        var state = new ParameterStartupCommandState();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(
            Substitute.For<IParameterStartupCommandContext>(), state, NullLogger<ParameterStartupCommandActor>.Instance));
        Assert.Equal("PARAM.EVIDENCE_REQUIRED", error.Message);
        Assert.Empty(state.Events);
        Assert.Equal(0, state.Revision);
    }

    [Fact]
    public void Replay_rejects_missing_creation_history_instead_of_silently_returning_partial_state()
    {
        Assert.Throws<InvalidDataException>(() => new ParameterSetCommandState().ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[]
            { new ParameterSetDraftSavedEvent { Revision = 2 } }));
        Assert.Throws<InvalidDataException>(() => new ParameterAssignmentCommandState().ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[]
            { new ParameterAssignmentChangedEvent { Revision = 2 } }));
        Assert.Throws<InvalidDataException>(() => new ParameterStartupCommandState().ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[]
            { new ParameterStartupChangedEvent { Revision = 2, RunId = Guid.NewGuid() } }));
    }

    [Fact]
    public async Task Release_and_report_reject_a_mismatched_frozen_plan_without_events()
    {
        var id = Guid.NewGuid();
        var run = new ParameterStartupRun(id, [], [], new(id, "current", "current", [], null), DateTime.UtcNow, "test");
        var state = new ParameterStartupCommandState();
        Assert.True(state.Update(new ParameterStartupCreatedEvent { EntityId = new(id), RunId = id, Revision = 1, Run = run }));
        state.AcceptChanges();
        var context = Substitute.For<IParameterStartupCommandContext>();
        var release = new ReleaseSignalStartupPlanCommand { CommandId = Guid.NewGuid(), RunId = id, EntityId = new(id), ExpectedFingerprint = "wrong" };
        var released = await release.ExecuteAsync(context, state, NullLogger<ParameterStartupCommandActor>.Instance);
        Assert.False(released.Success);
        Assert.Contains("PARAM.STARTUP_PLAN_CHANGED", released.ErrorMessage);
        var report = new RecordSignalStartupReportCommand { CommandId = Guid.NewGuid(), RunId = id, EntityId = new(id), ExpectedFingerprint = "wrong",
            Report = new(id, "current", new DateOnly(2026, 11, 3), "ES", DateTime.UtcNow, []) };
        var recorded = await report.ExecuteAsync(context, state, NullLogger<ParameterStartupCommandActor>.Instance);
        Assert.False(recorded.Success);
        Assert.Contains("PARAM.STARTUP_PLAN_CHANGED", recorded.ErrorMessage);
        Assert.Equal(1, state.Revision);
        Assert.Empty(state.Events);
        Assert.Single(state.ActiveRuns);
        Assert.Empty(state.Reports);
    }

    [Fact]
    public void Historical_eight_key_startup_audit_matches_per_run_retry_with_fresh_evidence()
    {
        var id = Guid.NewGuid();
        var registry = ParameterStartupEntityId.Registry;
        var historical = new ApplySignalStartupPlanCommand
        {
            CommandId = id, RunId = id, EntityId = registry,
            Subject = new(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, registry.Format())
        };
        var codec = new CommandAuditMessagePackCodec();
        var payload = FirstEightKeys(codec.Serialize(historical).Bytes);
        var audit = new CommandLogReadModel(id, historical.StreamId, default, historical.CommandName, DateTime.UtcNow, "",
            payload, (short)CommandAuditPayloadFormat.MessagePack, CommandAuditMessagePackCodec.CurrentVersion, SHA256.HashData(payload));
        var retry = historical with
        {
            EntityId = new(id), Subject = new(ActorType.Command, ApplySignalStartupPlanCommand.Actor, ApplySignalStartupPlanCommand.Verb, id.ToString("N")),
            Evidence = new(42, Assignments: [], Versions: [])
        };

        var decoded = (TomasAI.IFM.Shared.EventSourcing.ICommand)codec.Deserialize(typeof(ApplySignalStartupPlanCommand), payload,
            (short)CommandAuditPayloadFormat.MessagePack, CommandAuditMessagePackCodec.CurrentVersion);
        Assert.True(codec.MatchesStoredEncoding(decoded, payload));
        Assert.Equal(codec.Serialize(((TomasAI.IFM.Shared.EventSourcing.ICommandRetryIdentity)decoded).ForRetryIdentity()).Sha256,
            codec.Serialize(((TomasAI.IFM.Shared.EventSourcing.ICommandRetryIdentity)retry).ForRetryIdentity()).Sha256);
        var unknown = AppendUnknownField(codec.Serialize(historical).Bytes);
        var truncated = (TomasAI.IFM.Shared.EventSourcing.ICommand)codec.Deserialize(typeof(ApplySignalStartupPlanCommand), unknown,
            (short)CommandAuditPayloadFormat.MessagePack, CommandAuditMessagePackCodec.CurrentVersion);
        Assert.False(codec.MatchesStoredEncoding(truncated, unknown));

        ParameterStartupOperationModel.ValidateDuplicate(retry, audit);

        Assert.Throws<InvalidOperationException>(() => ParameterStartupOperationModel.ValidateDuplicate(retry with { ExpectedFingerprint = "changed" }, audit));
        var changed = payload.ToArray();
        changed[^1] ^= 1;
        Assert.Throws<InvalidOperationException>(() => ParameterStartupOperationModel.ValidateDuplicate(retry, audit with { CommandPayload = changed }));
    }

    static void AssertRepositories(Type context, Type expected)
    {
        var repositories = context.GetProperties().Select(property => property.PropertyType)
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEventSourceActorStateRepository<>))
            .Select(type => type.GenericTypeArguments.Single()).ToArray();
        Assert.Equal(new[] { expected }, repositories);
    }

    static byte[] FirstEightKeys(byte[] current)
    {
        var reader = new MessagePackReader(current);
        Assert.Equal(9, reader.ReadArrayHeader());
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(8);
        for (var index = 0; index < 8; index++)
            foreach (var segment in reader.ReadRaw()) writer.WriteRaw(segment.Span);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    static byte[] AppendUnknownField(byte[] current)
    {
        var reader = new MessagePackReader(current);
        var count = reader.ReadArrayHeader();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(count + 1);
        for (var index = 0; index < count; index++)
            foreach (var segment in reader.ReadRaw()) writer.WriteRaw(segment.Span);
        writer.WriteNil();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
