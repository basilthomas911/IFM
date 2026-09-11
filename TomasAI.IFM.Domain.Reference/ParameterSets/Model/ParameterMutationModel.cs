using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;
public static class ParameterMutationModel
{
    static string OriginalRequestHash(IParameterSetMutation command)=>ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new {
        command.CommandName,command.EntityId,command.ExpectedRevision,command.Version,command.ComponentCode,
        command.Name,command.Description,command.SchemaVersion,command.PayloadJson }));
    public static string RequestHash(IParameterSetMutation command)
    {
        var hash=OriginalRequestHash(command);
        return command is CreateParameterSetCommand{LegacySource:{} source}
            ?ParameterCanonicalPayloadModel.Hash(JsonSerializer.Serialize(new{OriginalHash=hash,LegacySource=source})):hash;
    }
    public static ParameterSetVersion Decide(IParameterSetMutation command,long revision,
        IReadOnlyDictionary<int,ParameterSetVersion> versions,DateTime now,bool? versionAssigned=null)
    {
        if(command.ExpectedRevision!=revision) throw new InvalidOperationException("PARAM.REVISION_CONFLICT");
        var descriptor=new RegimeDiscoveryParameterModel();
        if(command.ComponentCode!=descriptor.Summary.ComponentCode) throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED");
        if(command is CreateParameterSetCommand or SaveParameterDraftCommand)
        {
            if(command is CreateParameterSetCommand && versions.Count!=0) throw new InvalidOperationException("PARAM.ALREADY_EXISTS");
            if(command is SaveParameterDraftCommand && versions.Count==0) throw new InvalidOperationException("PARAM.NOT_FOUND");
            if(string.IsNullOrWhiteSpace(command.Name)||command.Name.Length>200||command.Description.Length>4000)
                throw new ArgumentException("PARAM.METADATA_INVALID");
            var json=ParameterCanonicalPayloadModel.Canonicalize(command.PayloadJson);
            var next=checked(versions.Keys.DefaultIfEmpty(0).Max()+1);
            if (!descriptor.Summary.SchemaVersions.Contains(command.SchemaVersion))
                throw new ArgumentException("PARAM.SCHEMA_UNSUPPORTED");
            var payload=JsonNode.Parse(json)!.AsObject();
            if (payload["SchemaVersion"]?.GetValue<int>() != command.SchemaVersion)
                throw new ArgumentException("PARAM.SCHEMA_MISMATCH");
            // The aggregate allocates identity/version; the working copy cannot override them.
            payload["ParameterSetId"]=command.EntityId.SetId;
            payload["Version"]=next;
            json=ParameterCanonicalPayloadModel.Canonicalize(payload.ToJsonString());
            var structural=ParameterSchemaRegistry.Default.ValidateStructure(command.ComponentCode,command.SchemaVersion,json);
            if(structural.Length!=0)throw new ArgumentException(string.Join("; ",structural.Select(x=>$"{x.Code}: {x.Path}: {x.Message}")));
            return new(new(command.EntityId.SetId,next,command.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),
                command.Name,command.Description,command.SchemaVersion,ParameterVersionStatus.Draft,json,now,command.OriginatedBy,CatalogRevision:revision+1,LegacySource:command is CreateParameterSetCommand create?create.LegacySource:versions.Values.First().LegacySource);
        }
        if(!versions.TryGetValue(command.Version,out var existing))throw new KeyNotFoundException("PARAM.NOT_FOUND");
        if(command is RenameParameterSetCommand)
        {
            if(string.IsNullOrWhiteSpace(command.Name)||command.Name.Length>200||command.Description.Length>4000)
                throw new ArgumentException("PARAM.METADATA_INVALID");
            return existing with {Name=command.Name.Trim(),Description=command.Description,CatalogRevision=revision+1};
        }
        if(command is PublishParameterVersionCommand)
        {
            var validation=new ParameterValidationReport(ParameterCanonicalPayloadModel.Hash(existing.PayloadJson),
                descriptor.Validate(existing.PayloadJson,existing.SchemaVersion));
            var errors=ParameterSetLifecycleModel.Publish(existing,validation);
            if(errors.Any(x=>x.Severity==ParameterIssueSeverity.Error))throw new ArgumentException(string.Join("; ",errors.Select(x=>x.Message)));
            return existing with {Status=ParameterVersionStatus.Published,PublishedAtUtc=now,CatalogRevision=revision+1};
        }
        if(command is RetireParameterVersionCommand)
        {
            if(versionAssigned is null)throw new InvalidOperationException("PARAM.RETIRE_REQUIRES_USAGE_CHECK");
            var errors=ParameterSetLifecycleModel.Retire(existing,versionAssigned.Value);
            if(errors.Length!=0)throw new InvalidOperationException(errors[0].Code);
            return existing with {Status=ParameterVersionStatus.Retired,RetiredAtUtc=now,CatalogRevision=revision+1};
        }
        // Renaming immutable version metadata is not supported; save a new draft instead.
        throw new InvalidOperationException("PARAM.OPERATION_UNSUPPORTED");
    }
}
