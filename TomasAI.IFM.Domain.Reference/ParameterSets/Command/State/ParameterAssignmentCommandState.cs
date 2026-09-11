using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;
public sealed class ParameterAssignmentCommandState:BaseEventSourceActorState<ParameterAssignmentCommandState>,IEventSourceActorState<ParameterAssignmentCommandState>
{
 public Dictionary<Guid,ParameterAuditEntry> Audit {get;}=[];

 public override ActorThreadId Id{get;set;}=default!;
 public ParameterAssignmentRevision? Assignment{get;private set;}
 public long Revision=>Assignment?.Revision??0;
 public Dictionary<Guid,string> Operations{get;}=[];
 protected override bool Apply(IEvent e)
 {
  if(e is not ParameterAssignmentChangedEvent fact||fact.Revision!=Revision+1)return false;
  var value=JsonSerializer.Deserialize<ParameterAssignmentRevision>(fact.AssignmentJson)??throw new InvalidDataException("Invalid assignment event.");
  if(value.Revision!=fact.Revision||value.AssignmentId!=fact.EntityId.AssignmentId)throw new InvalidDataException("Assignment identity mismatch.");
  if(!string.IsNullOrWhiteSpace(fact.AuditJson))
  {
   var entry=JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)??throw new InvalidDataException("Invalid audit entry.");
   if(entry.OperationId!=fact.CommandId||entry.Revision!=fact.Revision)throw new InvalidDataException("Audit identity mismatch.");
   Audit[fact.CommandId]=entry;
  }
  Assignment=value;Operations[fact.CommandId]=fact.RequestHash;return true;
 }
}
