using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
/// <summary>Duplicate startup commands may resume only the same audited operation.</summary>
public static class ParameterStartupOperationModel
{
    /// <summary>Compares business intent while allowing routing migration and refreshed uncommitted evidence.</summary>
    public static void ValidateDuplicate(IParameterStartupMutation command, CommandLogReadModel previous)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(previous);
        ICommand audited = command;
        var registry = ParameterStartupEntityId.Registry;
        var historical = new ActorSubject(ActorType.Command, ApplySignalStartupPlanCommand.Actor, command.Subject.Verb, registry.Format());
        if (string.Equals(previous.StreamId, historical.StreamId, StringComparison.Ordinal))
            audited = command switch
            {
                ApplySignalStartupPlanCommand apply => apply with { EntityId = registry, Subject = historical },
                ReleaseSignalStartupPlanCommand release => release with { EntityId = registry, Subject = historical },
                RecordSignalStartupReportCommand report => report with { EntityId = registry, Subject = historical },
                _ => throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH")
            };
        if (previous.CommandPayloadSha256 is not { Length: > 0 })
        {
            ValidateDuplicate((IParameterStartupMutation)audited, previous.CommandName, previous.StreamId, previous.CommandData);
            return;
        }
        if (previous.CommandName != command.CommandName || previous.StreamId != audited.StreamId
            || previous.CommandPayloadFormat != (short)CommandAuditPayloadFormat.MessagePack
            || previous.CommandPayloadVersion != CommandAuditMessagePackCodec.CurrentVersion
            || previous.CommandPayload is not { Length: > 0 }
            || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Security.Cryptography.SHA256.HashData(previous.CommandPayload), previous.CommandPayloadSha256))
            throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
        var codec = new CommandAuditMessagePackCodec();
        var stored = (ICommand)codec.Deserialize(command.GetType(), previous.CommandPayload,
            previous.CommandPayloadFormat.Value, previous.CommandPayloadVersion.Value);
        if (audited is ApplySignalStartupPlanCommand candidate && stored is ApplySignalStartupPlanCommand original)
            audited = candidate with { Evidence = original.Evidence };
        if (!codec.Matches(audited, codec.Serialize(stored).Sha256))
            throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
    }

    public static void ValidateDuplicate(IParameterStartupMutation command, string storedName, string storedStream, string json)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var document = JsonDocument.Parse(json); var value = document.RootElement;
        if (!string.Equals(storedName,command.CommandName,StringComparison.Ordinal) || !string.Equals(storedStream,command.StreamId,StringComparison.Ordinal) ||
         value.GetProperty("CommandId").GetGuid() != command.CommandId || value.GetProperty("RunId").GetGuid() != command.RunId ||
         !string.Equals(value.GetProperty("ExpectedFingerprint").GetString(),command.ExpectedFingerprint,StringComparison.Ordinal) ||
         value.GetProperty("EntityId").GetProperty("Id").GetGuid() != command.EntityId.Id ||
         value.GetProperty("PostEvents").GetBoolean() != command.PostEvents)
            throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
        if (command is RecordSignalStartupReportCommand report &&
         !System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(value.GetProperty("Report").GetRawText()), JsonSerializer.SerializeToNode(report.Report)))
            throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
    }
}
