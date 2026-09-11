using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public static class ParameterAuditModel
{
    public static string ForSet(IParameterSetMutation command, IReadOnlyDictionary<int,ParameterSetVersion> versions, ParameterSetVersion after)
    {
        var before=versions.GetValueOrDefault(after.Reference.Version) ?? versions.Values.OrderByDescending(x=>x.Reference.Version).FirstOrDefault();
        object? Metadata(ParameterSetVersion? value)=>value is null?null:new {value.Reference,value.Name,value.Description,value.SchemaVersion,value.Status};
        return JsonSerializer.Serialize(new ParameterAuditEntry(command.CommandId,command.EntityId.SetId,after.CatalogRevision,
            command.CommandName,command.OriginatedBy,DateTime.UtcNow,JsonSerializer.Serialize(Metadata(before)),JsonSerializer.Serialize(Metadata(after))));
    }
    public static string ForAssignment(Guid operationId,string action,string actorIdentity,ParameterAssignmentRevision? before,ParameterAssignmentRevision after)
        =>JsonSerializer.Serialize(new ParameterAuditEntry(operationId,after.AssignmentId,after.Revision,action,actorIdentity,
            DateTime.UtcNow,JsonSerializer.Serialize(before),JsonSerializer.Serialize(after)));
}
