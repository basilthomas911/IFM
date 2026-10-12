using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public static class ParameterMutationModel
{
    static string OriginalRequestHash(IParameterSetMutation command) => ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new
    {
        command.CommandName,
        command.EntityId,
        command.ExpectedRevision,
        command.Version,
        command.ComponentCode,
        command.Name,
        command.Description,
        command.SchemaVersion,
        command.PayloadJson
    }));
    public static string RequestHash(IParameterSetMutation command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var hash = OriginalRequestHash(command);
        return command is CreateParameterSetCommand { LegacySource: { } source }
            ? ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new { OriginalHash = hash, LegacySource = source })) : hash;
    }
    public static ParameterSetVersion Decide(IParameterSetMutation command, long revision,
        IReadOnlyDictionary<int, ParameterSetVersion> versions, DateTime now, bool? versionAssigned = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(versions);
        if (command.ExpectedRevision != revision) throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        var descriptor = ParameterComponentModelRegistry.Get(command.ComponentCode);
#pragma warning disable S3247 // C# 14 extension lowering diagnostic; this source has no redundant cast or local function to move.
        if (command is CreateParameterSetCommand or SaveParameterDraftCommand)
#pragma warning restore S3247
        {
            return CreateDraft(command, revision, versions, now, descriptor);
        }
        if (!versions.TryGetValue(command.Version, out var existing)) throw new KeyNotFoundException("PARAM.NOT_FOUND");
        if (command is RenameParameterSetCommand)
        {
            if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 200 || command.Description.Length > 4000)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.METADATA_INVALID");
#pragma warning restore MA0015
            return existing with { Name = command.Name.Trim(), Description = command.Description, CatalogRevision = revision + 1 };
        }
        if (command is PublishParameterVersionCommand)
        {
            var validation = new ParameterValidationReport(ParameterCanonicalPayloadModel.Hash(existing.PayloadJson),
                descriptor.Validate(existing.PayloadJson, existing.SchemaVersion));
            var errors = ParameterSetLifecycleModel.Publish(existing, validation);
            if (errors.Any(x => x.Severity == ParameterIssueSeverity.Error)) throw new ArgumentException(string.Join("; ", errors.Select(x => x.Message)),nameof(command));
            return existing with { Status = ParameterVersionStatus.Published, PublishedAtUtc = now, CatalogRevision = revision + 1 };
        }
        if (command is RetireParameterVersionCommand)
        {
            if (versionAssigned is null) throw new InvalidOperationException("PARAM.RETIRE_REQUIRES_USAGE_CHECK");
            var errors = ParameterSetLifecycleModel.Retire(existing, versionAssigned.Value);
            if (errors.Length != 0) throw new InvalidOperationException(errors[0].Code);
            return existing with { Status = ParameterVersionStatus.Retired, RetiredAtUtc = now, CatalogRevision = revision + 1 };
        }
        // Renaming immutable version metadata is not supported; save a new draft instead.
        throw new InvalidOperationException("PARAM.OPERATION_UNSUPPORTED");
    }    /// <summary>Allocates a new immutable draft version and validates the canonical payload without accepting client-supplied identity or version overrides.</summary>
    static ParameterSetVersion CreateDraft(IParameterSetMutation command, long revision,
        IReadOnlyDictionary<int, ParameterSetVersion> versions, DateTime now, IParameterComponentDescriptor descriptor)
    {
            if (command is CreateParameterSetCommand && versions.Count != 0) throw new InvalidOperationException("PARAM.ALREADY_EXISTS");
            if (command is SaveParameterDraftCommand && versions.Count == 0) throw new InvalidOperationException("PARAM.NOT_FOUND");
            if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > 200 || command.Description.Length > 4000)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.METADATA_INVALID");
#pragma warning restore MA0015
            var json = ParameterCanonicalPayloadModel.Canonicalize(command.PayloadJson);
            var next = checked(versions.Keys.DefaultIfEmpty(0).Max() + 1);
            if (!descriptor.Summary.SchemaVersions.Contains(command.SchemaVersion))
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.SCHEMA_UNSUPPORTED");
#pragma warning restore MA0015
            var payload = JsonNode.Parse(json)!.AsObject();
            if (payload["SchemaVersion"]?.GetValue<int>() != command.SchemaVersion)
#pragma warning disable MA0015 // The published rejection protocol requires an exact reason code in Exception.Message.
                throw new ArgumentException("PARAM.SCHEMA_MISMATCH");
#pragma warning restore MA0015
            // The aggregate allocates identity/version; the working copy cannot override them.
            payload["ParameterSetId"] = command.EntityId.SetId;
            payload["Version"] = next;
            json = ParameterCanonicalPayloadModel.Canonicalize(payload.ToJsonString());
            var structural = ParameterSchemaRegistry.Default.ValidateStructure(command.ComponentCode, command.SchemaVersion, json);
            if (structural.Length != 0) throw new ArgumentException(string.Join("; ", structural.Select(x => $"{x.Code}: {x.Path}: {x.Message}")),nameof(command));
            return new(new(command.EntityId.SetId, next, command.ComponentCode, ParameterCanonicalPayloadModel.Hash(json)),
                command.Name, command.Description, command.SchemaVersion, ParameterVersionStatus.Draft, json, now, command.OriginatedBy, CatalogRevision: revision + 1, LegacySource: command is CreateParameterSetCommand create ? create.LegacySource : versions.Values.First().LegacySource);
    }

}
