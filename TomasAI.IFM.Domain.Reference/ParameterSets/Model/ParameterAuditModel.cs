using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

public static class ParameterAuditModel
{
    public static ParameterAuditEntry ForSet(IParameterSetMutation command, IReadOnlyDictionary<int, ParameterSetVersion> versions, ParameterSetVersion after)
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(versions);
        var before = versions.GetValueOrDefault(after.Reference.Version) ?? versions.Values.OrderByDescending(x => x.Reference.Version).FirstOrDefault();
        static ParameterAuditValue? Metadata(ParameterSetVersion? value) => value is null ? null :
            new(value.Reference,value.Name,value.Description,value.SchemaVersion,value.Status);
        return new ParameterAuditEntry(command.CommandId,command.EntityId.SetId,after.CatalogRevision,
            command.CommandName,command.OriginatedBy,DateTime.UtcNow,string.Empty,string.Empty)
            {Before=Metadata(before),After=Metadata(after)};
    }
    public static ParameterAuditEntry ForAssignment(Guid operationId, string action, string actorIdentity, ParameterAssignmentRevision? before, ParameterAssignmentRevision after)
        
    {
        ArgumentNullException.ThrowIfNull(after);
        return new ParameterAuditEntry(operationId,after.AssignmentId,after.Revision,action,actorIdentity,DateTime.UtcNow,string.Empty,string.Empty)
            {Before=before is null ? null : new(Assignment:before),After=new(Assignment:after)};
    }
}
