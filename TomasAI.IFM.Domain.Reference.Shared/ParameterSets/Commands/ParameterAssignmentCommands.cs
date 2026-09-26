using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
[MessagePackObject]
public readonly record struct ParameterAssignmentEntityId([property:Key(0)] Guid AssignmentId):IActorEntityId
{public string Format()=>AssignmentId.ToString("N");}
