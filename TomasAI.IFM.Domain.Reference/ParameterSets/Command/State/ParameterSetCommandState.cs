using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
public sealed class ParameterSetCommandState:BaseEventSourceActorState<ParameterSetCommandState>,IEventSourceActorState<ParameterSetCommandState>
{
 public Dictionary<Guid,ParameterAuditEntry> Audit {get;}=[];

    public override ActorThreadId Id {get;set;}=default!;
    public long CatalogRevision {get;private set;}
    public Dictionary<int,ParameterSetVersion> Versions {get;}=[];
    public Dictionary<Guid,string> Operations {get;}=[];
    public Dictionary<Guid,ParameterOperationReceipt> Receipts {get;}=[];
    protected override bool Apply(IEvent domainEvent)
    {
        if(domainEvent is not IParameterSetFact fact || fact.Revision!=CatalogRevision+1) return false;
        var version=JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson)
            ??throw new InvalidDataException("Missing committed parameter version.");
        Versions[version.Reference.Version]=version;
        // Set metadata is shared across versions; immutable payloads are never rewritten.
        foreach(var key in Versions.Keys.ToArray())
            Versions[key]=Versions[key] with {Name=version.Name,Description=version.Description,CatalogRevision=fact.Revision};
        Operations[fact.CommandId]=fact.RequestHash;
        Receipts[fact.CommandId]=new(fact.CommandId,fact.RequestHash,fact.Revision,version.Reference);
  if(!string.IsNullOrWhiteSpace(fact.AuditJson))
  {
   var entry=JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)??throw new InvalidDataException("Invalid audit entry.");
   if(entry.OperationId!=fact.CommandId||entry.Revision!=fact.Revision)throw new InvalidDataException("Audit identity mismatch.");
   Audit[fact.CommandId]=entry;
  }
        CatalogRevision=fact.Revision;
        return true;
    }
}
